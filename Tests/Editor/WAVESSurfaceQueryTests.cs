using MadeYellow.WAVES.AudioVisualEffects;
using MadeYellow.WAVES.Surfaces;
using NUnit.Framework;
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

        static readonly RaycastHit DefaultHit = default;

        static WAVESQuery RememberingQuery(float lifetime)
        {
            var cache = new WAVESSurfaceCache();
            cache.Enabled = true;
            cache.Lifetime = lifetime;
            return new WAVESQuery(cache);
        }
    }
}
