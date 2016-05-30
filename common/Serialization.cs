using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

using UnityEngine;

namespace Kfp
{
    public static class DiffEqualityComparer
    {
        private const double Epsilon = 9E-5;

        public static bool Equal(Vector3d a, Vector3d b) {
            return
                NearlyEqual(a.x, b.x, Epsilon) &&
                NearlyEqual(a.y, b.y, Epsilon) &&
                NearlyEqual(a.z, b.z, Epsilon);
        }

        public static bool Equal(Vector3 a, Vector3 b) {
            return
                NearlyEqual(a.x, b.x, Epsilon) &&
                NearlyEqual(a.y, b.y, Epsilon) &&
                NearlyEqual(a.z, b.z, Epsilon);
        }

        public static bool Equal(Quaternion a, Quaternion b) {
            return
                NearlyEqual(a.x, b.x, Epsilon) &&
                NearlyEqual(a.y, b.y, Epsilon) &&
                NearlyEqual(a.z, b.z, Epsilon) &&
                NearlyEqual(a.w, b.w, Epsilon);
        }

        // TODO: This is not actually being called right now, but it needs to be.
        public static bool Equal<T>(T a, T b) {
            return EqualityComparer<T>.Default.Equals(a, b);
        }

        public static bool Equal(String a, String b) {
            return string.Equals(a, b);
        }

        private static bool NearlyEqual(double a, double b) {
            double epsilon = Math.Max(Math.Abs(a), Math.Abs(b)) * 1E-15;
            return Math.Abs(a - b) <= epsilon;
        }

        private static bool NearlyEqual(double a, double b, double epsilon) {
            return Math.Abs(a - b) <= epsilon;
        }

        private static bool NearlyEqual(float a, float b) {
            float epsilon = Math.Max(Math.Abs(a), Math.Abs(b)) * 1E-7f;
            return Math.Abs(a - b) <= epsilon;
        }

        private static bool NearlyEqual(float a, float b, float epsilon) {
            return Math.Abs(a - b) <= epsilon;
        }
    }

    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = false)]
    public class DiffAttribute : Attribute
    {
        public DiffAttribute(int index) {
            Index = index;
        }

        public int Index { get; set; }
    }

    public class DiffSerializer
    {
        public static Diff<T> Deserialize<T>(BinaryReader r)
            where T : struct
        {
            var diff = new Diff<T>();
            diff.Changed = r.ReadInt32();
            var deserializer = Introspection.GetDeserializer<T>();
            deserializer(r, ref diff.Item);
            return diff;
        }

        public static void Serialize<T>(BinaryWriter w, Diff<T> diff)
            where T : struct
        {
            w.Write(diff.Changed);
            var serializer = Introspection.GetSerializer<T>();
            serializer(w, diff.Changed, ref diff.Item);
        }
    }

    public static class Diff
    {
        public static Diff<T> Create<T>(T? oldItem, T newItem) where T : struct {
            if (!oldItem.HasValue) {
                return new Diff<T> {
                    Changed = -1,
                    Item = newItem,
                };
            }

            var differ = Introspection.GetDiffer<T>();
            var oldItemValue = oldItem.Value;
            return new Diff<T> {
                Changed = differ(ref oldItemValue, ref newItem),
                Item = newItem,
            };
        }
    }

    public struct Diff<T> where T : struct
    {
        public int Changed;
        public T Item;

        public bool Apply(ref T target)
        {
            if (Changed == 0) {
                return false;
            }

            var applier = Introspection.GetApplier<T>();
            applier(Changed, ref Item, ref target);
            return true;
        }
    }

    internal class TypeCache
    {
        private Dictionary<Type, object> _cache =
            new Dictionary<Type, object>();

        public U GetOrCreate<T, U>(Func<U> creator) {
            object cached;
            if (_cache.TryGetValue(typeof(T), out cached)) {
                return (U)cached;
            }

            var obj = creator();
            _cache[typeof(T)] = obj;
            return obj;
        }
    }

    static class Introspection
    {
        private static TypeCache _appliers = new TypeCache();
        private static TypeCache _differs = new TypeCache();
        private static TypeCache _serializers = new TypeCache();
        private static TypeCache _deserializers = new TypeCache();

        public delegate void Applier<T>(int changed, ref T changes, ref T target);
        public delegate int Differ<T>(ref T fromItem, ref T toItem);
        public delegate void Serializer<T>(
            BinaryWriter writer, int changed, ref T item)
            where T : struct;
        public delegate void Deserializer<T>(BinaryReader reader, ref T item)
            where T : struct;

        public static Applier<T> GetApplier<T>() {
            return _appliers.GetOrCreate<T, Applier<T>>(BuildApplier<T>);
        }

        public static Differ<T> GetDiffer<T>() {
            return _differs.GetOrCreate<T, Differ<T>>(BuildDiffer<T>);
        }

        public static Serializer<T> GetSerializer<T>() where T : struct {
            return _serializers.GetOrCreate<T, Serializer<T>>(BuildSerializer<T>);
        }

        public static Deserializer<T> GetDeserializer<T>() where T: struct {
            return _deserializers.GetOrCreate<T, Deserializer<T>>(
                BuildDeserializer<T>);
        }

        private static Applier<T> BuildApplier<T>() {
            var type = typeof(T);
            var m = new DynamicMethod(
                "applier_" + type.Name,
                typeof(void),
                new Type[] {
                    typeof(int),
                    type.MakeByRefType(),
                    type.MakeByRefType()
                },
                type.Module, true);

            var il = m.GetILGenerator();

            foreach (var member in GetInterestingMembers(type)) {
                var attr = GetAttr(member);

                var merge = il.DefineLabel();

                // if ((changed & (1 << index)) != 0)
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, 1 << attr.Index);
                il.Emit(OpCodes.And);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Beq, merge);

                // copy value
                il.Emit(OpCodes.Ldarg_2);

                il.Emit(OpCodes.Ldarg_1);
                EmitGet(il, member);

                EmitSet(il, member);

                // end if
                il.MarkLabel(merge);
            }

            il.Emit(OpCodes.Ret);

            return (Applier<T>)m.CreateDelegate(typeof(Applier<T>));
        }

        private static DiffAttribute GetAttr(MemberInfo member) {
            var attrs = member.GetCustomAttributes(typeof(DiffAttribute), true);
            if (attrs.Length == 0) {
                var msg = string.Format("has no {0}", typeof(DiffAttribute).Name);
                throw new ArgumentException("member", msg);
            }
            return (DiffAttribute)attrs[0];
        }

        private static void EmitGet(ILGenerator il, MemberInfo member) {
            // REVIEW: Add more thorough handling for reference types,
            // structs-by-value?
            switch (member.MemberType) {
                case MemberTypes.Field:
                    il.Emit(OpCodes.Ldfld, (FieldInfo)member);
                    break;
                case MemberTypes.Property: {
                    // FIXME: Get properties to work.
                    var getter = ((PropertyInfo)member).GetGetMethod();
                    il.Emit(OpCodes.Callvirt, getter);
                    break;
                }
                default:
                    throw new NotSupportedException(member.MemberType.ToString());
            }
        }

        private static void EmitSet(ILGenerator il, MemberInfo member) {
            // REVIEW: Add more thorough handling for reference types,
            // structs-by-value?
            switch (member.MemberType) {
                case MemberTypes.Field:
                    il.Emit(OpCodes.Stfld, (FieldInfo)member);
                    break;
                case MemberTypes.Property: {
                    var setter = ((PropertyInfo)member).GetSetMethod();
                    il.Emit(OpCodes.Callvirt, setter);
                    break;
                }
                default:
                    throw new NotSupportedException(member.MemberType.ToString());
            }
        }

        private static void EmitWrite(ILGenerator il, MemberInfo member) {
            Type writeType;
            switch (member.MemberType) {
                case MemberTypes.Field:
                    writeType = ((FieldInfo)member).FieldType;
                    break;
                case MemberTypes.Property: {
                    writeType = ((PropertyInfo)member).PropertyType;
                    break;
                }
                default:
                    throw new NotSupportedException(member.MemberType.ToString());
            }

            var writeMethod = typeof(DiffSerializer)
                .GetMethod("Write", new[] {typeof(BinaryWriter), writeType});

            il.Emit(OpCodes.Callvirt, writeMethod);
        }

        private static void EmitEq(ILGenerator il, MemberInfo member, Label label)
        {
            Type type;
            switch (member.MemberType) {
                case MemberTypes.Field:
                    type = ((FieldInfo)member).FieldType;
                    break;
                case MemberTypes.Property:
                    type = ((PropertyInfo)member).PropertyType;
                    break;
                default:
                    throw new NotSupportedException(member.MemberType.ToString());
            }

            // TODO: Make EmitEq nicer.

            var equal = typeof(DiffEqualityComparer).GetMethod(
                "Equal", new Type[] { type, type }
            );

            if (equal != null) {
                il.Emit(OpCodes.Call, equal);
                il.Emit(OpCodes.Ldc_I4_1);
            }
            il.Emit(OpCodes.Beq, label);
        }

        private static Differ<T> BuildDiffer<T>() {
            var type = typeof(T);
            var m = new DynamicMethod(
                "differ_" + type.Name,
                typeof(int),
                new Type[] {
                    type.MakeByRefType(),
                    type.MakeByRefType(),
                },
                type.Module, true);

            var il = m.GetILGenerator();
            // Push accumulator
            il.Emit(OpCodes.Ldc_I4_0);
            foreach (var member in GetInterestingMembers(type)) {
                var attr = GetAttr(member);

                var merge = il.DefineLabel();

                // if (get(a) != get(b))
                il.Emit(OpCodes.Ldarg_0);
                EmitGet(il, member);
                il.Emit(OpCodes.Ldarg_1);
                EmitGet(il, member);
                EmitEq(il, member, merge);

                // accum |= (1 << index)
                il.Emit(OpCodes.Ldc_I4, 1 << attr.Index);
                il.Emit(OpCodes.Or);

                // end if
                il.MarkLabel(merge);
            }

            il.Emit(OpCodes.Ret);

            return (Differ<T>)m.CreateDelegate(typeof(Differ<T>));
        }

        private static Serializer<T> BuildSerializer<T>() where T : struct {
            var type = typeof(T);
            var m = new DynamicMethod(
                "serializer_" + type.Name,
                typeof(void),
                new Type[] {
                    typeof(BinaryWriter),
                    typeof(int),
                    type.MakeByRefType(),
                },
                type.Module, true);

            var il = m.GetILGenerator();

            foreach (var member in GetInterestingMembers(type)) {
                var attr = GetAttr(member);

                var merge = il.DefineLabel();

                // if ((changed & (1 << index)) != 0)
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, 1 << attr.Index);
                il.Emit(OpCodes.And);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Beq, merge);

                // write value
                il.Emit(OpCodes.Ldarg_0);

                il.Emit(OpCodes.Ldarg_2);
                EmitGet(il, member);

                EmitWrite(il, member);

                // end if
                il.MarkLabel(merge);
            }

            il.Emit(OpCodes.Ret);

            return (Serializer<T>)m.CreateDelegate(typeof(Serializer<T>));
        }

        private static Deserializer<T> BuildDeserializer<T>() where T : struct {
            return (BinaryReader r, ref T t) => {};
        }

        private static IEnumerable<MemberInfo> GetInterestingMembers(Type type)
        {
            var bindingFlags = BindingFlags.Public | BindingFlags.Instance;

            foreach (var member in type.GetMembers(bindingFlags)) {
                if (member.MemberType != MemberTypes.Field &&
                    member.MemberType != MemberTypes.Property) {
                    continue;
                }

                var attrs = member.GetCustomAttributes(typeof(DiffAttribute), true);
                if (attrs.Length > 0) {
                    yield return member;
                }
            }
        }
    }
}
