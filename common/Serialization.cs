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
        public static bool Equal(Vector3d a, Vector3d b) {
            return
                NearlyEqual(a.x, b.x, 1E-4) &&
                NearlyEqual(a.y, b.y, 1E-4) &&
                NearlyEqual(a.z, b.z, 1E-4);
        }

        public static bool Equal(Vector3 a, Vector3 b) {
            return
                NearlyEqual(a.x, b.x, 1E-4f) &&
                NearlyEqual(a.y, b.y, 1E-4f) &&
                NearlyEqual(a.z, b.z, 1E-4f);
        }

        public static bool Equal(Quaternion a, Quaternion b) {
            return
                NearlyEqual(a.x, b.x, 1E-4f) &&
                NearlyEqual(a.y, b.y, 1E-4f) &&
                NearlyEqual(a.z, b.z, 1E-4f) &&
                NearlyEqual(a.w, b.w, 1E-4f);
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
        public static void Write<T>(BinaryWriter w, Diff<T> diff)
            where T : struct
        {
            w.Write(diff.Changed);

            // var bindingFlags = BindingFlags.Public | BindingFlags.Instance;

            // var fields = typeof(T).GetFields(bindingFlags);
            // var props = typeof(T).GetProperties(bindingFlags);

            // var attrs = Attribute.getCustomAttributes
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

    static class Introspection
    {
        private static Dictionary<Type, object> _appliers =
            new Dictionary<Type, object>();

        private static Dictionary<Type, object> _differs =
            new Dictionary<Type, object>();

        public delegate int Differ<T>(ref T fromItem, ref T toItem);
        public delegate void Applier<T>(int changed, ref T changes, ref T target);

        public static Applier<T> GetApplier<T>() {
            object cached;
            if (_appliers.TryGetValue(typeof(T), out cached)) {
                return (Applier<T>)cached;
            }

            var applier = BuildApplier<T>();
            _appliers[typeof(T)] = applier;
            return applier;
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

            foreach (var member in GetInterestingMembers<T>()) {
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

        public static Differ<T> GetDiffer<T>() {
            object cached;
            if (_differs.TryGetValue(typeof(T), out cached)) {
                return (Differ<T>)cached;
            }

            var differ = BuildDiffer<T>();
            _differs[typeof(T)] = differ;
            return differ;
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
            foreach (var member in GetInterestingMembers<T>()) {
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

        private static IEnumerable<MemberInfo> GetInterestingMembers<T>()
        {
            var bindingFlags = BindingFlags.Public | BindingFlags.Instance;

            foreach (var member in typeof(T).GetMembers(bindingFlags)) {
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
