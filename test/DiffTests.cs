using System;

using NUnit.Framework;

namespace Kfp.Tests
{
    [TestFixture]
    public class DiffTests
    {
        [Test]
        public void ChangedIntFieldIsReported() {
            var a = new Foo { X = 1 };
            var b = new Foo { X = 2 };
            var diff = MagicDiff.Create(a, b);

            Assert.That(diff.Changed, Is.EqualTo(1));
        }

        [Test]
        public void UnchangedIntFieldIsNotReported() {
            var a = new Foo { X = 1 };
            var b = new Foo { X = 1 };
            var diff = MagicDiff.Create(a, b);

            Assert.That(diff.Changed, Is.EqualTo(0));
        }

        [Test]
        public void ChangedFieldsAreApplied() {
            var a = new Foo { X = 1 };
            var b = new Foo { X = 2 };
            var diff = MagicDiff.Create(a, b);
            diff.Apply(ref a);
            Assert.That(a.X, Is.EqualTo(2));
        }

        [Test]
        public void UnchangedFieldsAreNotApplied() {
            var a = new Foo { X = 1 };
            var diff = MagicDiff.Create(a, a);
            a.X = 10;
            diff.Apply(ref a);

            Assert.That(a.X, Is.EqualTo(10));
        }

        struct Foo
        {
            [Magic(0)]
            public int X;
        }
    }
}
