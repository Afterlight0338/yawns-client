// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Database;
using Realms;
using Realms.Schema;

namespace osu.Game.Tests.Database
{
    /// <summary>
    /// YAWNS: lazer's library is shared, so a database with a different structure must be refused and left untouched.
    /// </summary>
    [TestFixture]
    public class SharedLibrarySchemaTest
    {
        private string directory = null!;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"yawns-schema-{Guid.NewGuid()}");
            Directory.CreateDirectory(directory);
            RealmAccess.RefuseSchemaChanges = true;
        }

        [TearDown]
        public void TearDown()
        {
            RealmAccess.RefuseSchemaChanges = false;
            Directory.Delete(directory, true);
        }

        [TestCase(1000UL, "newer")]
        [TestCase(1UL, "older")]
        public void TestDifferentStructureIsRefused(ulong fileSchema, string expected)
        {
            string path = Path.Combine(directory, OsuGameBase.CLIENT_DATABASE_FILENAME);

            var config = new RealmConfiguration(path)
            {
                SchemaVersion = fileSchema,
                Schema = new RealmSchema.Builder { new ObjectSchema.Builder("Fake") { Property.Primitive("Id", RealmValueType.Int) } },
            };

            using (Realm.GetInstance(config))
            {
            }

            byte[] before = File.ReadAllBytes(path);

            var e = Assert.Throws<SharedLibraryMismatchException>(() => new RealmAccess(new NativeStorage(directory), OsuGameBase.CLIENT_DATABASE_FILENAME).Dispose());

            Assert.That(e!.Message, Does.Contain(expected));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(directory, "*corrupt*"), Is.Empty);
            Assert.That(Directory.GetFiles(directory, "*newer_version*"), Is.Empty);
        }

        [Test]
        public void TestSameStructureOpens()
        {
            using (new RealmAccess(new NativeStorage(directory), OsuGameBase.CLIENT_DATABASE_FILENAME))
            {
            }

            using (new RealmAccess(new NativeStorage(directory), OsuGameBase.CLIENT_DATABASE_FILENAME))
            {
            }
        }
    }
}
