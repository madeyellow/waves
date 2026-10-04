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
    }
}
