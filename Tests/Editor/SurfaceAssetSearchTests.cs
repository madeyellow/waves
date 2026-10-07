using System;
using System.Collections.Generic;
using MadeYellow.WAVES.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Tests.Editor
{
    public class SurfaceAssetSearchTests
    {
        string _root;
        Material _grassRocks;
        Material _stone;
        Material _nestedGrass;
        Material _nestedMetal;
        Texture2D _grassAlbedo;
        Cubemap _grassReflection;
        Texture2D _stoneAlbedo;

        [SetUp]
        public void SetUp()
        {
            string folder = "__WAVESSurfaceSearch_" + Guid.NewGuid().ToString("N");
            _root = "Assets/" + folder;
            AssetDatabase.CreateFolder("Assets", folder);
            AssetDatabase.CreateFolder(_root, "Nested");

            _grassRocks = CreateMaterial(_root + "/grass_with_rocks_01_color.mat");
            _stone = CreateMaterial(_root + "/stone_wall.mat");
            _nestedGrass = CreateMaterial(_root + "/Nested/grass_patch.mat");
            _nestedMetal = CreateMaterial(_root + "/Nested/metal_panel.mat");
            _grassAlbedo = CreateTexture(_root + "/grass_albedo.asset");
            _grassReflection = CreateCubemap(_root + "/grass_reflection.asset");
            _stoneAlbedo = CreateTexture(_root + "/stone_albedo.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_root) && AssetDatabase.IsValidFolder(_root))
                AssetDatabase.DeleteAsset(_root);

            AssetDatabase.Refresh();
        }

        [Test]
        public void Find_MatchesMaterialsByACaseInsensitiveSubstring()
        {
            var found = new List<UnityEngine.Object>();
            SurfaceAssetSearch.Find(true, _root, new[] { "GRASS" }, found);

            CollectionAssert.AreEquivalent(new UnityEngine.Object[] { _grassRocks, _nestedGrass }, found);
        }

        [Test]
        public void Find_MatchesTexturesIncludingCubemaps()
        {
            var found = new List<UnityEngine.Object>();
            SurfaceAssetSearch.Find(false, _root, new[] { "grass" }, found);

            CollectionAssert.AreEquivalent(new UnityEngine.Object[] { _grassAlbedo, _grassReflection }, found);
        }

        [Test]
        public void Find_LetsAStarStandForAnyText()
        {
            var found = new List<UnityEngine.Object>();
            SurfaceAssetSearch.Find(true, _root, new[] { "*rocks*" }, found);

            Assert.AreEqual(1, found.Count);
            Assert.AreEqual(_grassRocks, found[0]);
        }

        [Test]
        public void Find_KeepsAssetsAlreadyInTheList()
        {
            var found = new List<UnityEngine.Object> { _stone };
            SurfaceAssetSearch.Find(true, _root, new[] { "grass" }, found);

            Assert.AreSame(_stone, found[0]);
            Assert.IsTrue(found.Contains(_grassRocks));
            Assert.IsTrue(found.Contains(_nestedGrass));
            Assert.IsFalse(found.Contains(_nestedMetal));
        }

        [Test]
        public void Find_AddsNothingWithoutKeywords()
        {
            var empty = new List<UnityEngine.Object>();
            var missing = new List<UnityEngine.Object>();
            SurfaceAssetSearch.Find(true, _root, new string[0], empty);
            SurfaceAssetSearch.Find(true, _root, null, missing);
            SurfaceAssetSearch.Find(false, _root, null, null);

            Assert.AreEqual(0, empty.Count);
            Assert.AreEqual(0, missing.Count);
        }

        [Test]
        public void Collect_TakesEveryAssetInTheFolderTree()
        {
            var materials = new List<UnityEngine.Object>();
            var textures = new List<UnityEngine.Object>();
            SurfaceAssetSearch.Collect(true, _root, materials);
            SurfaceAssetSearch.Collect(false, _root, textures);

            CollectionAssert.AreEquivalent(
                new UnityEngine.Object[] { _grassRocks, _stone, _nestedGrass, _nestedMetal },
                materials);
            CollectionAssert.AreEquivalent(
                new UnityEngine.Object[] { _grassAlbedo, _grassReflection, _stoneAlbedo },
                textures);
        }

        [Test]
        public void Collect_AddsNothingForAnEmptyFolder()
        {
            var found = new List<UnityEngine.Object> { _stone };
            SurfaceAssetSearch.Collect(true, string.Empty, found);
            SurfaceAssetSearch.Collect(false, null, null);

            Assert.AreEqual(1, found.Count);
            Assert.AreSame(_stone, found[0]);
        }

        static Material CreateMaterial(string path)
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Shader shader = probe.GetComponent<MeshRenderer>().sharedMaterial.shader;
            UnityEngine.Object.DestroyImmediate(probe);
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Texture2D CreateTexture(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        static Cubemap CreateCubemap(string path)
        {
            var cubemap = new Cubemap(2, TextureFormat.RGBA32, false);
            AssetDatabase.CreateAsset(cubemap, path);
            return cubemap;
        }
    }
}
