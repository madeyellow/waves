using System;
using MadeYellow.WAVES.AudioVisualEffects;
using MadeYellow.WAVES.Surfaces;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Tests.Editor
{
    public class WAVESSurfaceQueryTests
    {
        [TearDown]
        public void TearDown()
        {
            SurfaceRegistry.ResetForTests();
        }

        [Test]
        public void TryResolve_MarkerWinsOverAnUnmarkedCollider()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var marked = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var plain = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider markedCollider = marked.GetComponent<Collider>();
            Collider plainCollider = plain.GetComponent<Collider>();
            SurfaceRegistry.RegisterMarker(markedCollider.GetEntityId(), marked.GetEntityId(), surface);

            bool foundMarked = SurfaceRegistry.TryResolve(markedCollider, marked.transform.position, out SurfaceTypeDefinition markedType);
            bool foundPlain = SurfaceRegistry.TryResolve(plainCollider, plain.transform.position, out SurfaceTypeDefinition plainType);

            Assert.IsTrue(foundMarked);
            Assert.AreSame(surface, markedType);
            Assert.IsFalse(foundPlain);
            Assert.IsNull(plainType);

            Object.DestroyImmediate(marked);
            Object.DestroyImmediate(plain);
            Object.DestroyImmediate(surface);
        }

        [Test]
        public void TryGetSurface_ReadsTheMarkerOnAHit()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(9000f, 0f, 9000f);
            Collider collider = cube.GetComponent<Collider>();
            SurfaceRegistry.RegisterMarker(collider.GetEntityId(), cube.GetEntityId(), surface);
            Physics.SyncTransforms();

            bool hit = Physics.Raycast(cube.transform.position + Vector3.up * 3f, Vector3.down, out RaycastHit ray, 6f);
            Assert.IsTrue(hit);
            Assert.AreSame(collider, ray.collider);

            var query = new WAVESQuery();
            bool found = query.TryGetSurface(in ray, out SurfaceTypeDefinition queried);
            Assert.IsTrue(found);
            Assert.AreSame(surface, queried);

            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(surface);
        }

        [Test]
        public void TryGetSurface_RemembersAMarkerUntilTheLifetimeEnds()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider collider = cube.GetComponent<Collider>();
            EntityId colliderId = collider.GetEntityId();
            EntityId ownerId = cube.GetEntityId();
            SurfaceRegistry.RegisterMarker(colliderId, ownerId, surface);
            WAVESQuery query = RememberingQuery(1f);

            bool first = query.TryGetSurface(collider, Vector3.zero, 0f, out SurfaceTypeDefinition cached);
            SurfaceRegistry.UnregisterMarker(colliderId, ownerId);
            bool during = query.TryGetSurface(collider, Vector3.zero, 1f, out SurfaceTypeDefinition held);
            bool after = query.TryGetSurface(collider, Vector3.zero, 1.01f, out SurfaceTypeDefinition expired);

            Assert.IsTrue(first);
            Assert.AreSame(surface, cached);
            Assert.IsTrue(during);
            Assert.AreSame(surface, held);
            Assert.IsFalse(after);
            Assert.IsNull(expired);

            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(surface);
        }

        [Test]
        public void TryGetSurface_RemembersAColliderWithNoSurface()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider collider = cube.GetComponent<Collider>();
            EntityId colliderId = collider.GetEntityId();
            EntityId ownerId = cube.GetEntityId();
            WAVESQuery query = RememberingQuery(1f);

            bool missed = query.TryGetSurface(collider, Vector3.zero, 0f, out SurfaceTypeDefinition none);
            SurfaceRegistry.RegisterMarker(colliderId, ownerId, surface);
            bool during = query.TryGetSurface(collider, Vector3.zero, 0.5f, out SurfaceTypeDefinition held);
            bool after = query.TryGetSurface(collider, Vector3.zero, 1.01f, out SurfaceTypeDefinition fresh);

            Assert.IsFalse(missed);
            Assert.IsNull(none);
            Assert.IsFalse(during);
            Assert.IsNull(held);
            Assert.IsTrue(after);
            Assert.AreSame(surface, fresh);

            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(surface);
        }

        [Test]
        public void TryGetSurface_SkipsTheCacheWhenItIsOff()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider collider = cube.GetComponent<Collider>();
            EntityId colliderId = collider.GetEntityId();
            EntityId ownerId = cube.GetEntityId();
            SurfaceRegistry.RegisterMarker(colliderId, ownerId, surface);
            var cache = new WAVESSurfaceCache();
            var query = new WAVESQuery(cache);

            bool first = query.TryGetSurface(collider, Vector3.zero, 0f, out SurfaceTypeDefinition cached);
            SurfaceRegistry.UnregisterMarker(colliderId, ownerId);
            bool second = query.TryGetSurface(collider, Vector3.zero, 0f, out SurfaceTypeDefinition latest);

            Assert.IsTrue(first);
            Assert.AreSame(surface, cached);
            Assert.IsFalse(second);
            Assert.IsNull(latest);

            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(surface);
        }

        [Test]
        public void TryGetSurface_KeepsEachColliderSeparate()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var marked = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var plain = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider markedCollider = marked.GetComponent<Collider>();
            Collider plainCollider = plain.GetComponent<Collider>();
            SurfaceRegistry.RegisterMarker(markedCollider.GetEntityId(), marked.GetEntityId(), surface);
            WAVESQuery query = RememberingQuery(1f);

            bool foundMarked = query.TryGetSurface(markedCollider, Vector3.zero, 0f, out SurfaceTypeDefinition markedType);
            bool foundPlain = query.TryGetSurface(plainCollider, Vector3.zero, 0f, out SurfaceTypeDefinition plainType);

            Assert.IsTrue(foundMarked);
            Assert.AreSame(surface, markedType);
            Assert.IsFalse(foundPlain);
            Assert.IsNull(plainType);

            Object.DestroyImmediate(marked);
            Object.DestroyImmediate(plain);
            Object.DestroyImmediate(surface);
        }

        [Test]
        public void TryGetSurface_DoesNotRememberATerrainSample()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var ground = new GameObject("terrain");
            var map = ground.AddComponent<TerrainSurfaceMap>();
            Collider collider = ground.GetComponent<TerrainCollider>();
            EntityId colliderId = collider.GetEntityId();
            SurfaceRegistry.RegisterTerrain(colliderId, map);
            WAVESQuery query = RememberingQuery(10f);

            bool missed = query.TryGetSurface(collider, Vector3.zero, 0f, out SurfaceTypeDefinition first);
            SurfaceRegistry.UnregisterTerrain(colliderId, map);
            SurfaceRegistry.RegisterMarker(colliderId, ground.GetEntityId(), surface);
            bool found = query.TryGetSurface(collider, Vector3.zero, 0f, out SurfaceTypeDefinition second);

            Assert.IsFalse(missed);
            Assert.IsNull(first);
            Assert.IsTrue(found);
            Assert.AreSame(surface, second);

            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(surface);
        }

        [Test]
        public void TryGetSurface_ReadsCacheSettingsFromTheComponent()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var host = new GameObject("waves");
            var waves = host.AddComponent<global::MadeYellow.WAVES.AudioVisualEffects.WAVES>();
            waves.CacheSurfaces = true;
            waves.SurfaceCacheLifetime = 2f;
            Collider collider = cube.GetComponent<Collider>();
            EntityId colliderId = collider.GetEntityId();
            EntityId ownerId = cube.GetEntityId();
            SurfaceRegistry.RegisterMarker(colliderId, ownerId, surface);
            var query = new WAVESQuery(waves, new WAVESSurfaceCache());

            bool first = query.TryGetSurface(collider, Vector3.zero, 0f, out SurfaceTypeDefinition cached);
            SurfaceRegistry.UnregisterMarker(colliderId, ownerId);
            bool during = query.TryGetSurface(collider, Vector3.zero, 2f, out SurfaceTypeDefinition held);
            waves.CacheSurfaces = false;
            bool live = query.TryGetSurface(collider, Vector3.zero, 2f, out SurfaceTypeDefinition latest);

            Assert.IsTrue(first);
            Assert.AreSame(surface, cached);
            Assert.IsTrue(during);
            Assert.AreSame(surface, held);
            Assert.IsFalse(live);
            Assert.IsNull(latest);

            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(surface);
        }

        [Test]
        public void TryGetSurface_ReturnsFalseWhenTheColliderIsMissing()
        {
            var query = new WAVESQuery();
            bool found = query.TryGetSurface(null, Vector3.one, out SurfaceTypeDefinition surface);
            bool timed = query.TryGetSurface(null, Vector3.one, 0f, out SurfaceTypeDefinition timedSurface);
            bool missedRay = query.TryGetSurface(in DefaultHit, out SurfaceTypeDefinition raySurface);

            Assert.IsFalse(found);
            Assert.IsNull(surface);
            Assert.IsFalse(timed);
            Assert.IsNull(timedSurface);
            Assert.IsFalse(missedRay);
            Assert.IsNull(raySurface);
        }

        [Test]
        public void TryResolve_IgnoresANullColliderAndANullSurface()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider collider = cube.GetComponent<Collider>();
            SurfaceRegistry.RegisterMarker(collider.GetEntityId(), cube.GetEntityId(), null);

            bool missingCollider = SurfaceRegistry.TryResolve(null, Vector3.zero, out SurfaceTypeDefinition missing);
            bool unmarked = SurfaceRegistry.TryResolve(collider, Vector3.zero, out SurfaceTypeDefinition plain);

            Assert.IsFalse(missingCollider);
            Assert.IsNull(missing);
            Assert.IsFalse(unmarked);
            Assert.IsNull(plain);

            Object.DestroyImmediate(cube);
        }

        [Test]
        public void TryResolve_IgnoresANullTerrainMap()
        {
            var ground = new GameObject("terrain");
            Collider collider = ground.AddComponent<TerrainCollider>();
            SurfaceRegistry.RegisterTerrain(collider.GetEntityId(), null);

            bool found = SurfaceRegistry.TryResolve(collider, Vector3.zero, out SurfaceTypeDefinition surface);

            Assert.IsFalse(found);
            Assert.IsNull(surface);

            Object.DestroyImmediate(ground);
        }

        [Test]
        public void TryResolve_KeepsAMarkerWhenAnotherOwnerUnregistersIt()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var other = new GameObject("other");
            Collider collider = cube.GetComponent<Collider>();
            EntityId colliderId = collider.GetEntityId();
            SurfaceRegistry.RegisterMarker(colliderId, cube.GetEntityId(), surface);
            SurfaceRegistry.UnregisterMarker(colliderId, other.GetEntityId());

            bool found = SurfaceRegistry.TryResolve(collider, Vector3.zero, out SurfaceTypeDefinition resolved);

            Assert.IsTrue(found);
            Assert.AreSame(surface, resolved);

            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(other);
            Object.DestroyImmediate(surface);
        }

        [Test]
        public void TryGetSurface_PublicCallRemembersAMarker()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider collider = cube.GetComponent<Collider>();
            EntityId colliderId = collider.GetEntityId();
            EntityId ownerId = cube.GetEntityId();
            SurfaceRegistry.RegisterMarker(colliderId, ownerId, surface);
            WAVESQuery query = RememberingQuery(60f);

            bool first = query.TryGetSurface(collider, Vector3.zero, out SurfaceTypeDefinition cached);
            SurfaceRegistry.UnregisterMarker(colliderId, ownerId);
            bool second = query.TryGetSurface(collider, Vector3.zero, out SurfaceTypeDefinition held);

            Assert.IsTrue(first);
            Assert.AreSame(surface, cached);
            Assert.IsTrue(second);
            Assert.AreSame(surface, held);

            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(surface);
        }

        [Test]
        public void TryGetSurface_ReplacesAMarkerAfterTheLifetimeEnds()
        {
            SurfaceTypeDefinition firstSurface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition secondSurface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider collider = cube.GetComponent<Collider>();
            EntityId colliderId = collider.GetEntityId();
            EntityId ownerId = cube.GetEntityId();
            SurfaceRegistry.RegisterMarker(colliderId, ownerId, firstSurface);
            WAVESQuery query = RememberingQuery(1f);

            bool first = query.TryGetSurface(collider, Vector3.zero, 0f, out SurfaceTypeDefinition cached);
            SurfaceRegistry.UnregisterMarker(colliderId, ownerId);
            SurfaceRegistry.RegisterMarker(colliderId, ownerId, secondSurface);
            bool during = query.TryGetSurface(collider, Vector3.up, 1f, out SurfaceTypeDefinition held);
            bool after = query.TryGetSurface(collider, Vector3.zero, 1.01f, out SurfaceTypeDefinition fresh);

            Assert.IsTrue(first);
            Assert.AreSame(firstSurface, cached);
            Assert.IsTrue(during);
            Assert.AreSame(firstSurface, held);
            Assert.IsTrue(after);
            Assert.AreSame(secondSurface, fresh);

            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(firstSurface);
            Object.DestroyImmediate(secondSurface);
        }

        [Test]
        public void TryGetSurface_KeepsTwoMarkedCollidersApart()
        {
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider leftCollider = left.GetComponent<Collider>();
            Collider rightCollider = right.GetComponent<Collider>();
            SurfaceRegistry.RegisterMarker(leftCollider.GetEntityId(), left.GetEntityId(), grass);
            SurfaceRegistry.RegisterMarker(rightCollider.GetEntityId(), right.GetEntityId(), stone);
            WAVESQuery query = RememberingQuery(1f);

            bool foundLeft = query.TryGetSurface(leftCollider, Vector3.zero, 0f, out SurfaceTypeDefinition leftType);
            bool foundRight = query.TryGetSurface(rightCollider, Vector3.zero, 0f, out SurfaceTypeDefinition rightType);
            SurfaceRegistry.UnregisterMarker(leftCollider.GetEntityId(), left.GetEntityId());
            bool heldLeft = query.TryGetSurface(leftCollider, Vector3.zero, 0f, out SurfaceTypeDefinition held);
            bool stillRight = query.TryGetSurface(rightCollider, Vector3.zero, 0f, out SurfaceTypeDefinition unchanged);

            Assert.IsTrue(foundLeft);
            Assert.AreSame(grass, leftType);
            Assert.IsTrue(foundRight);
            Assert.AreSame(stone, rightType);
            Assert.IsTrue(heldLeft);
            Assert.AreSame(grass, held);
            Assert.IsTrue(stillRight);
            Assert.AreSame(stone, unchanged);

            Object.DestroyImmediate(left);
            Object.DestroyImmediate(right);
            Object.DestroyImmediate(grass);
            Object.DestroyImmediate(stone);
        }

        [Test]
        public void TryGetSurface_SamplesATerrainAtEveryPoint()
        {
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var ground = new GameObject("terrain");
            var map = ground.AddComponent<TerrainSurfaceMap>();
            map.UseMapForTests(new[] { grass, stone }, new byte[] { 0, 1 }, 2, 1);
            TerrainData data = ground.GetComponent<Terrain>().terrainData;
            Collider collider = ground.GetComponent<TerrainCollider>();
            SurfaceRegistry.RegisterTerrain(collider.GetEntityId(), map);
            WAVESQuery query = RememberingQuery(60f);
            var onGrass = new Vector3(0.25f, 0f, 0.25f);
            var onStone = new Vector3(1.5f, 0f, 0.25f);

            bool foundGrass = query.TryGetSurface(collider, onGrass, 0f, out SurfaceTypeDefinition grassType);
            bool foundStone = query.TryGetSurface(collider, onStone, 0f, out SurfaceTypeDefinition stoneType);
            bool outside = query.TryGetSurface(collider, new Vector3(5f, 0f, 0.25f), 0f, out SurfaceTypeDefinition none);
            map.UseMapForTests(new[] { grass, stone }, new byte[] { 1, 0 }, 2, 1);
            bool changed = query.TryGetSurface(collider, onGrass, 0f, out SurfaceTypeDefinition next);

            Assert.IsTrue(foundGrass);
            Assert.AreSame(grass, grassType);
            Assert.IsTrue(foundStone);
            Assert.AreSame(stone, stoneType);
            Assert.IsFalse(outside);
            Assert.IsNull(none);
            Assert.IsTrue(changed);
            Assert.AreSame(stone, next);

            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(grass);
            Object.DestroyImmediate(stone);
        }

        [Test]
        public void TryGetSurface_PrefersAMarkerAndThenTheTerrain()
        {
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var ground = new GameObject("terrain");
            var map = ground.AddComponent<TerrainSurfaceMap>();
            map.UseMapForTests(new[] { grass }, new byte[] { 0 }, 1, 1);
            TerrainData data = ground.GetComponent<Terrain>().terrainData;
            Collider collider = ground.GetComponent<TerrainCollider>();
            EntityId colliderId = collider.GetEntityId();
            EntityId ownerId = ground.GetEntityId();
            SurfaceRegistry.RegisterTerrain(colliderId, map);
            SurfaceRegistry.RegisterMarker(colliderId, ownerId, stone);
            WAVESQuery query = RememberingQuery(1f);

            bool marked = query.TryGetSurface(collider, new Vector3(0.25f, 0f, 0.25f), 0f, out SurfaceTypeDefinition markerType);
            SurfaceRegistry.UnregisterMarker(colliderId, ownerId);
            bool during = query.TryGetSurface(collider, new Vector3(0.25f, 0f, 0.25f), 1f, out SurfaceTypeDefinition held);
            bool after = query.TryGetSurface(collider, new Vector3(0.25f, 0f, 0.25f), 1.01f, out SurfaceTypeDefinition terrainType);

            Assert.IsTrue(marked);
            Assert.AreSame(stone, markerType);
            Assert.IsTrue(during);
            Assert.AreSame(stone, held);
            Assert.IsTrue(after);
            Assert.AreSame(grass, terrainType);

            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(grass);
            Object.DestroyImmediate(stone);
        }

        [Test]
        public void TryGetSurface_UsesAShorterLifetimeOnTheNextRead()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var host = new GameObject("waves");
            var waves = host.AddComponent<global::MadeYellow.WAVES.AudioVisualEffects.WAVES>();
            waves.CacheSurfaces = true;
            waves.SurfaceCacheLifetime = 5f;
            Collider collider = cube.GetComponent<Collider>();
            EntityId colliderId = collider.GetEntityId();
            EntityId ownerId = cube.GetEntityId();
            SurfaceRegistry.RegisterMarker(colliderId, ownerId, surface);
            var query = new WAVESQuery(waves, new WAVESSurfaceCache());

            bool first = query.TryGetSurface(collider, Vector3.zero, 0f, out SurfaceTypeDefinition cached);
            SurfaceRegistry.UnregisterMarker(colliderId, ownerId);
            waves.SurfaceCacheLifetime = 1f;
            bool during = query.TryGetSurface(collider, Vector3.zero, 1f, out SurfaceTypeDefinition held);
            bool after = query.TryGetSurface(collider, Vector3.zero, 1.01f, out SurfaceTypeDefinition expired);

            Assert.IsTrue(first);
            Assert.AreSame(surface, cached);
            Assert.IsTrue(during);
            Assert.AreSame(surface, held);
            Assert.IsFalse(after);
            Assert.IsNull(expired);

            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(surface);
        }

        [Test]
        public void TryGetSurface_RemembersAgainAfterCachingIsTurnedBackOn()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var host = new GameObject("waves");
            var waves = host.AddComponent<global::MadeYellow.WAVES.AudioVisualEffects.WAVES>();
            waves.CacheSurfaces = false;
            waves.SurfaceCacheLifetime = 5f;
            Collider collider = cube.GetComponent<Collider>();
            EntityId colliderId = collider.GetEntityId();
            EntityId ownerId = cube.GetEntityId();
            SurfaceRegistry.RegisterMarker(colliderId, ownerId, surface);
            var query = new WAVESQuery(waves, new WAVESSurfaceCache());

            bool live = query.TryGetSurface(collider, Vector3.zero, 0f, out SurfaceTypeDefinition first);
            SurfaceRegistry.UnregisterMarker(colliderId, ownerId);
            waves.CacheSurfaces = true;
            bool missed = query.TryGetSurface(collider, Vector3.zero, 0f, out SurfaceTypeDefinition none);
            SurfaceRegistry.RegisterMarker(colliderId, ownerId, surface);
            bool during = query.TryGetSurface(collider, Vector3.zero, 1f, out SurfaceTypeDefinition held);

            Assert.IsTrue(live);
            Assert.AreSame(surface, first);
            Assert.IsFalse(missed);
            Assert.IsNull(none);
            Assert.IsFalse(during);
            Assert.IsNull(held);

            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(surface);
        }

        [Test]
        public void SurfaceCacheLifetime_StartsAtSixtySecondsAndRejectsShorterThanAHundredth()
        {
            var host = new GameObject("waves");
            var waves = host.AddComponent<global::MadeYellow.WAVES.AudioVisualEffects.WAVES>();

            Assert.IsTrue(waves.CacheSurfaces);
            Assert.AreEqual(60f, waves.SurfaceCacheLifetime);
            waves.SurfaceCacheLifetime = 0f;
            Assert.AreEqual(0.01f, waves.SurfaceCacheLifetime);

            Object.DestroyImmediate(host);
        }

        [Test]
        public void TryGetSurface_ReadsAMaterialWhenNothingIsMarked()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = UniqueMaterial(cube.GetComponent<MeshRenderer>());
            surface.UseBindingsForTests(new[] { material }, null);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(surface, resolved);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void TryGetSurface_ReadsAMaterialAfterTheRegistryIsCleared()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = UniqueMaterial(cube.GetComponent<MeshRenderer>());
            surface.UseBindingsForTests(new[] { material }, null);
            SurfaceRegistry.ForgetDefinitionsForTests();
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(surface, resolved);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void TryGetSurface_LoadsASavedSurfaceTypeThatNoSceneObjectReferences()
        {
            string folder = "__WAVESSurfaceReload_" + Guid.NewGuid().ToString("N");
            string root = "Assets/" + folder;
            AssetDatabase.CreateFolder("Assets", folder);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = UniqueMaterial(cube.GetComponent<MeshRenderer>());
            AssetDatabase.CreateAsset(material, root + "/grass.mat");
            var surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            AssetDatabase.CreateAsset(surface, root + "/GrassFromDisk.asset");
            surface.UseBindingsForTests(new[] { material }, null);
            EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssets();
            SurfaceRegistry.ForgetDefinitionsForTests();
            Resources.UnloadAsset(surface);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreEqual("GrassFromDisk", resolved.name);
                Assert.AreEqual(material, resolved.GetMaterial(0));
            }
            finally
            {
                Object.DestroyImmediate(cube);
                if (AssetDatabase.IsValidFolder(root))
                    AssetDatabase.DeleteAsset(root);
            }
        }

        [Test]
        public void TryGetSurface_ReadsTheFirstMatchingMaterial()
        {
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            Material first = UniqueMaterial(renderer);
            Material second = new Material(first.shader);
            renderer.sharedMaterials = new[] { first, second };
            grass.UseBindingsForTests(new[] { first }, null);
            stone.UseBindingsForTests(new[] { second }, null);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(grass, resolved);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(grass);
                Object.DestroyImmediate(stone);
            }
        }

        [Test]
        public void TryGetSurface_PrefersTheEarlierSurfaceWhenMaterialsOverlap()
        {
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = UniqueMaterial(cube.GetComponent<MeshRenderer>());
            grass.UseBindingsForTests(new[] { material }, null);
            stone.UseBindingsForTests(new[] { material }, null);
            grass.SetOrder(5);
            stone.SetOrder(1);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(stone, resolved);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(grass);
                Object.DestroyImmediate(stone);
            }
        }

        [Test]
        public void TryGetSurface_ReadsATextureWhenTheMaterialIsNotListed()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = UniqueMaterial(cube.GetComponent<MeshRenderer>());
            var texture = new Texture2D(2, 2);
            AssignTexture(material, texture);
            surface.UseBindingsForTests(null, new Texture[] { texture });
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(surface, resolved);
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void TryGetSurface_PrefersAListedMaterialOverATexture()
        {
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = UniqueMaterial(cube.GetComponent<MeshRenderer>());
            var texture = new Texture2D(2, 2);
            AssignTexture(material, texture);
            grass.UseBindingsForTests(null, new Texture[] { texture });
            stone.UseBindingsForTests(new[] { material }, null);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(stone, resolved);
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(grass);
                Object.DestroyImmediate(stone);
            }
        }

        [Test]
        public void TryGetSurface_ReadsAChildMeshWhenTheColliderHasNoRenderer()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var root = new GameObject("ground");
            var collider = root.AddComponent<BoxCollider>();
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(child.GetComponent<Collider>());
            Material material = UniqueMaterial(child.GetComponent<MeshRenderer>());
            surface.UseBindingsForTests(new[] { material }, null);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(collider, Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(surface, resolved);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void TryGetSurface_PrefersAMarkerOverAMaterial()
        {
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider collider = cube.GetComponent<Collider>();
            Material material = UniqueMaterial(cube.GetComponent<MeshRenderer>());
            grass.UseBindingsForTests(new[] { material }, null);
            SurfaceRegistry.RegisterMarker(collider.GetEntityId(), cube.GetEntityId(), stone);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(collider, Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(stone, resolved);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(grass);
                Object.DestroyImmediate(stone);
            }
        }

        [Test]
        public void TryGetSurface_DoesNotUseMaterialsWhenATerrainSampleMisses()
        {
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var ground = new GameObject("terrain");
            var map = ground.AddComponent<TerrainSurfaceMap>();
            map.UseMapForTests(new[] { grass }, new byte[] { 0 }, 1, 1);
            TerrainData data = ground.GetComponent<Terrain>().terrainData;
            Collider collider = ground.GetComponent<TerrainCollider>();
            var renderer = ground.AddComponent<MeshRenderer>();
            Material material = NewMaterial();
            renderer.sharedMaterial = material;
            stone.UseBindingsForTests(new[] { material }, null);
            SurfaceRegistry.RegisterTerrain(collider.GetEntityId(), map);
            var query = new WAVESQuery();
            try
            {
                bool inside = query.TryGetSurface(collider, new Vector3(0.25f, 0f, 0.25f), out SurfaceTypeDefinition terrainType);
                bool outside = query.TryGetSurface(collider, new Vector3(5f, 0f, 0.25f), out SurfaceTypeDefinition none);
                Assert.IsTrue(inside);
                Assert.AreSame(grass, terrainType);
                Assert.IsFalse(outside);
                Assert.IsNull(none);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(ground);
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(grass);
                Object.DestroyImmediate(stone);
            }
        }

        [Test]
        public void TryGetSurface_RemembersAMaterialUntilTheLifetimeEnds()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider collider = cube.GetComponent<Collider>();
            Material material = UniqueMaterial(cube.GetComponent<MeshRenderer>());
            surface.UseBindingsForTests(new[] { material }, null);
            WAVESQuery query = RememberingQuery(1f);
            try
            {
                bool first = query.TryGetSurface(collider, Vector3.zero, 0f, out SurfaceTypeDefinition cached);
                surface.UseBindingsForTests(null, null);
                bool during = query.TryGetSurface(collider, Vector3.zero, 1f, out SurfaceTypeDefinition held);
                bool after = query.TryGetSurface(collider, Vector3.zero, 1.01f, out SurfaceTypeDefinition expired);
                Assert.IsTrue(first);
                Assert.AreSame(surface, cached);
                Assert.IsTrue(during);
                Assert.AreSame(surface, held);
                Assert.IsFalse(after);
                Assert.IsNull(expired);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void TryGetSurface_ReadsASkinnedMeshOnTheCollider()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var body = new GameObject("body");
            var collider = body.AddComponent<BoxCollider>();
            var skinned = body.AddComponent<SkinnedMeshRenderer>();
            Material material = NewMaterial();
            skinned.sharedMaterial = material;
            surface.UseBindingsForTests(new[] { material }, null);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(collider, Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(surface, resolved);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(body);
                Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void TryGetSurface_ReadsAParentRendererWhenTheColliderHasNone()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var parent = new GameObject("parent");
            var renderer = parent.AddComponent<MeshRenderer>();
            Material material = NewMaterial();
            renderer.sharedMaterial = material;
            var child = new GameObject("child");
            child.transform.SetParent(parent.transform, false);
            var collider = child.AddComponent<BoxCollider>();
            surface.UseBindingsForTests(new[] { material }, null);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(collider, Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(surface, resolved);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void TryGetSurface_PrefersTheColliderRendererOverAChild()
        {
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var root = new GameObject("root");
            var collider = root.AddComponent<BoxCollider>();
            var own = root.AddComponent<MeshRenderer>();
            Material stoneMaterial = NewMaterial();
            own.sharedMaterial = stoneMaterial;
            var child = new GameObject("child");
            child.transform.SetParent(root.transform, false);
            var childRenderer = child.AddComponent<MeshRenderer>();
            Material grassMaterial = NewMaterial();
            childRenderer.sharedMaterial = grassMaterial;
            stone.UseBindingsForTests(new[] { stoneMaterial }, null);
            grass.UseBindingsForTests(new[] { grassMaterial }, null);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(collider, Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(stone, resolved);
            }
            finally
            {
                Object.DestroyImmediate(stoneMaterial);
                Object.DestroyImmediate(grassMaterial);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(stone);
                Object.DestroyImmediate(grass);
            }
        }

        [Test]
        public void TryGetSurface_PrefersAChildRendererOverAParent()
        {
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var parent = new GameObject("parent");
            var parentRenderer = parent.AddComponent<MeshRenderer>();
            Material stoneMaterial = NewMaterial();
            parentRenderer.sharedMaterial = stoneMaterial;
            var middle = new GameObject("middle");
            middle.transform.SetParent(parent.transform, false);
            var collider = middle.AddComponent<BoxCollider>();
            var child = new GameObject("child");
            child.transform.SetParent(middle.transform, false);
            var childRenderer = child.AddComponent<MeshRenderer>();
            Material grassMaterial = NewMaterial();
            childRenderer.sharedMaterial = grassMaterial;
            stone.UseBindingsForTests(new[] { stoneMaterial }, null);
            grass.UseBindingsForTests(new[] { grassMaterial }, null);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(collider, Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(grass, resolved);
            }
            finally
            {
                Object.DestroyImmediate(stoneMaterial);
                Object.DestroyImmediate(grassMaterial);
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(stone);
                Object.DestroyImmediate(grass);
            }
        }

        [Test]
        public void TryGetSurface_SkipsNullMaterialsAndStillReadsTheNextOne()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            Material material = UniqueMaterial(renderer);
            renderer.sharedMaterials = new Material[] { null, material };
            surface.UseBindingsForTests(new Material[] { null, material }, null);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(surface, resolved);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void TryGetSurface_ChecksEveryMaterialBeforeAnyTexture()
        {
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            Material first = UniqueMaterial(renderer);
            Material second = new Material(first.shader);
            var texture = new Texture2D(2, 2);
            AssignTexture(first, texture);
            renderer.sharedMaterials = new[] { first, second };
            grass.UseBindingsForTests(null, new Texture[] { texture });
            stone.UseBindingsForTests(new[] { second }, null);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(stone, resolved);
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(grass);
                Object.DestroyImmediate(stone);
            }
        }

        [Test]
        public void TryGetSurface_ReadsATextureOnALaterMaterial()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            Material bare = UniqueMaterial(renderer);
            Material textured = new Material(bare.shader);
            var texture = new Texture2D(2, 2);
            AssignTexture(textured, texture);
            renderer.sharedMaterials = new[] { bare, textured };
            surface.UseBindingsForTests(null, new Texture[] { texture });
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(surface, resolved);
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(bare);
                Object.DestroyImmediate(textured);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void TryGetSurface_PrefersTheEarlierSurfaceWhenTexturesOverlap()
        {
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = UniqueMaterial(cube.GetComponent<MeshRenderer>());
            var texture = new Texture2D(2, 2);
            AssignTexture(material, texture);
            grass.UseBindingsForTests(null, new Texture[] { texture });
            stone.UseBindingsForTests(null, new Texture[] { texture });
            grass.SetOrder(4);
            stone.SetOrder(0);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(stone, resolved);
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(grass);
                Object.DestroyImmediate(stone);
            }
        }

        [Test]
        public void TryGetSurface_UsesTheNameWhenTheOrderMatches()
        {
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            grass.name = "Grass";
            stone.name = "Stone";
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = UniqueMaterial(cube.GetComponent<MeshRenderer>());
            grass.UseBindingsForTests(new[] { material }, null);
            stone.UseBindingsForTests(new[] { material }, null);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(grass, resolved);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(grass);
                Object.DestroyImmediate(stone);
            }
        }

        [Test]
        public void TryGetSurface_DropsASurfaceWhenItsDefinitionIsDestroyed()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = UniqueMaterial(cube.GetComponent<MeshRenderer>());
            surface.UseBindingsForTests(new[] { material }, null);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(surface, resolved);
                Object.DestroyImmediate(surface);
                bool after = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition none);
                Assert.IsFalse(after);
                Assert.IsNull(none);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                if (surface != null)
                    Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void TryGetSurface_ReadsAMaterialFromARaycastHit()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(9100f, 0f, 9100f);
            Material material = UniqueMaterial(cube.GetComponent<MeshRenderer>());
            surface.UseBindingsForTests(new[] { material }, null);
            Physics.SyncTransforms();
            var query = new WAVESQuery();
            try
            {
                bool hit = Physics.Raycast(cube.transform.position + Vector3.up * 3f, Vector3.down, out RaycastHit ray, 6f);
                Assert.IsTrue(hit);
                bool found = query.TryGetSurface(ray, out SurfaceTypeDefinition resolved);
                bool missed = query.TryGetSurface(DefaultHit, out SurfaceTypeDefinition none);
                Assert.IsTrue(found);
                Assert.AreSame(surface, resolved);
                Assert.IsFalse(missed);
                Assert.IsNull(none);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void TryGetSurface_PrefersAMarkerOverATerrainSample()
        {
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var ground = new GameObject("terrain");
            var map = ground.AddComponent<TerrainSurfaceMap>();
            map.UseMapForTests(new[] { grass }, new byte[] { 0 }, 1, 1);
            TerrainData data = ground.GetComponent<Terrain>().terrainData;
            Collider collider = ground.GetComponent<TerrainCollider>();
            SurfaceRegistry.RegisterTerrain(collider.GetEntityId(), map);
            SurfaceRegistry.RegisterMarker(collider.GetEntityId(), ground.GetEntityId(), stone);
            var query = new WAVESQuery();
            try
            {
                bool found = query.TryGetSurface(collider, new Vector3(0.25f, 0f, 0.25f), out SurfaceTypeDefinition resolved);
                Assert.IsTrue(found);
                Assert.AreSame(stone, resolved);
            }
            finally
            {
                Object.DestroyImmediate(ground);
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(grass);
                Object.DestroyImmediate(stone);
            }
        }

        [Test]
        public void TryGetSurface_SamplesTerrainAgainInsideTheCacheLifetime()
        {
            SurfaceTypeDefinition grass = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            SurfaceTypeDefinition stone = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var ground = new GameObject("terrain");
            var map = ground.AddComponent<TerrainSurfaceMap>();
            map.UseMapForTests(new[] { grass }, new byte[] { 0 }, 1, 1);
            TerrainData data = ground.GetComponent<Terrain>().terrainData;
            Collider collider = ground.GetComponent<TerrainCollider>();
            SurfaceRegistry.RegisterTerrain(collider.GetEntityId(), map);
            WAVESQuery query = RememberingQuery(60f);
            try
            {
                bool first = query.TryGetSurface(collider, new Vector3(0.25f, 0f, 0.25f), 0f, out SurfaceTypeDefinition terrainType);
                map.UseMapForTests(new[] { stone }, new byte[] { 0 }, 1, 1);
                bool second = query.TryGetSurface(collider, new Vector3(0.25f, 0f, 0.25f), 1f, out SurfaceTypeDefinition updated);
                Assert.IsTrue(first);
                Assert.AreSame(grass, terrainType);
                Assert.IsTrue(second);
                Assert.AreSame(stone, updated);
            }
            finally
            {
                Object.DestroyImmediate(ground);
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(grass);
                Object.DestroyImmediate(stone);
            }
        }

        [Test]
        public void TryGetSurface_RemembersAMissUntilTheLifetimeEnds()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider collider = cube.GetComponent<Collider>();
            Material material = UniqueMaterial(cube.GetComponent<MeshRenderer>());
            WAVESQuery query = RememberingQuery(1f);
            try
            {
                bool missed = query.TryGetSurface(collider, Vector3.zero, 0f, out SurfaceTypeDefinition none);
                surface.UseBindingsForTests(new[] { material }, null);
                bool during = query.TryGetSurface(collider, Vector3.zero, 1f, out SurfaceTypeDefinition held);
                bool after = query.TryGetSurface(collider, Vector3.zero, 1.01f, out SurfaceTypeDefinition resolved);
                Assert.IsFalse(missed);
                Assert.IsNull(none);
                Assert.IsFalse(during);
                Assert.IsNull(held);
                Assert.IsTrue(after);
                Assert.AreSame(surface, resolved);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void TryGetSurface_SeesANewTextureBindingOnTheNextLookup()
        {
            SurfaceTypeDefinition surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = UniqueMaterial(cube.GetComponent<MeshRenderer>());
            var texture = new Texture2D(2, 2);
            AssignTexture(material, texture);
            var query = new WAVESQuery();
            try
            {
                bool missed = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition none);
                surface.UseBindingsForTests(null, new Texture[] { texture });
                bool found = query.TryGetSurface(cube.GetComponent<Collider>(), Vector3.zero, out SurfaceTypeDefinition resolved);
                Assert.IsFalse(missed);
                Assert.IsNull(none);
                Assert.IsTrue(found);
                Assert.AreSame(surface, resolved);
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(surface);
            }
        }

        static readonly RaycastHit DefaultHit = default;

        static Material UniqueMaterial(MeshRenderer renderer)
        {
            var material = new Material(renderer.sharedMaterial.shader);
            renderer.sharedMaterial = material;
            return material;
        }

        static Material NewMaterial()
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Shader shader = probe.GetComponent<MeshRenderer>().sharedMaterial.shader;
            Object.DestroyImmediate(probe);
            return new Material(shader);
        }

        static void AssignTexture(Material material, Texture texture)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            else if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            else
                material.mainTexture = texture;
        }

        static WAVESQuery RememberingQuery(float lifetime)
        {
            var cache = new WAVESSurfaceCache();
            cache.Enabled = true;
            cache.Lifetime = lifetime;
            return new WAVESQuery(cache);
        }
    }
}
