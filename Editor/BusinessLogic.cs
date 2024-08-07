using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KiriMeshSplitter
{
    internal static class BusinessLogic
    {
        public static void SplitByMaterials (SkinnedMeshRenderer meshRenderer)
        {
            var submeshDir = GetSubmeshPath (meshRenderer);
            CreateFolder (submeshDir);

            for (var i = 0; i < meshRenderer.sharedMesh.subMeshCount; i++) {
                SplitSubmesh (meshRenderer, i, submeshDir);
            }

            meshRenderer.gameObject.SetActive (false);
        }

        private static string GetSubmeshPath (SkinnedMeshRenderer meshRenderer)
        {
            var meshPath = AssetDatabase.GetAssetPath (meshRenderer.sharedMesh);
            var baseDir = Path.GetDirectoryName (meshPath);

            if (baseDir.EndsWith ("Submeshes")) {
                return baseDir;
            } else {
                return Path.Combine (baseDir, "Submeshes");
            }
        }

        private static void CreateFolder (string path)
        {
            if (AssetDatabase.IsValidFolder (path)) {
                return;
            }

            var parent = Path.GetDirectoryName (path);
            var dirname = Path.GetFileName (path);

            if (!AssetDatabase.IsValidFolder (parent)) {
                CreateFolder (parent);
            }

            AssetDatabase.CreateFolder (parent, dirname);
        }

        private static void SplitSubmesh (SkinnedMeshRenderer meshRenderer, int index, string submeshDir)
        {
            var materialName = meshRenderer.sharedMaterials [index].name;
            var meshName = meshRenderer.gameObject.name + "_" + materialName;
            var tris = new List<int>();
            meshRenderer.sharedMesh.GetTriangles(tris, index);
            var triangles = new List<List<int>> { tris };

            var newMeshRenderer = CreateNewMesh (meshRenderer, triangles, submeshDir, meshName);
            newMeshRenderer.sharedMaterials = new[] { meshRenderer.sharedMaterials [index] };
        }

        private static GameObject CloneObject (GameObject gameObject)
        {
            return Object.Instantiate (gameObject, gameObject.transform.parent);
        }

        public static void SplitByQuad (SkinnedMeshRenderer meshRenderer, MeshFilter meshFilter)
        {
            var plane = CreatePlane (meshFilter);
            var mesh = meshRenderer.sharedMesh;
            var matrix = meshRenderer.transform.localToWorldMatrix;

            var meshName = meshRenderer.gameObject.name;
            var submeshDir = GetSubmeshPath (meshRenderer);
            CreateFolder (submeshDir);

            var triA = new List<List<int>> ();
            var triB = new List<List<int>> ();

            // 取得するたびにいちいちコピーが走り、遅いのでローカル変数に突っ込んでそれを読む
            var tris = mesh.vertices;

            for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++) {
                var triangles = mesh.GetTriangles (subMeshIndex);
                triA.Add (new List<int> ());
                triB.Add (new List<int> ());

                for (var triangleIndex = 0; triangleIndex < triangles.Length; triangleIndex += 3) {
                    var triangle = triangles.Skip (triangleIndex).Take (3).ToList();
                    var side = triangle.Any(n => plane.GetSide(matrix.MultiplyPoint(tris[n])));

                    if (side) {
                        triA [subMeshIndex].AddRange (triangle);
                    } else {
                        triB [subMeshIndex].AddRange (triangle);
                    }

                    if (triangleIndex % 30 == 0) {
                        EditorUtility.DisplayProgressBar (
                            "処理中", 
                            string.Format ("submesh:{0}/{1}, triangles:{2}/{3}", subMeshIndex, mesh.subMeshCount, triangleIndex, triangles.Length),
                            (float)triangleIndex / triangles.Length
                        );
                    }
                }
            }

            CreateNewMesh (meshRenderer, triA, submeshDir, meshName + "_a");
            CreateNewMesh (meshRenderer, triB, submeshDir, meshName + "_b");
            meshRenderer.gameObject.SetActive (false);

            EditorUtility.ClearProgressBar (); 
        }

        private static SkinnedMeshRenderer CreateNewMesh (SkinnedMeshRenderer original, List<List<int>> triangles, string dirname, string name)
        {
            var gameObject = CloneObject (original.gameObject);
            gameObject.name = name;
            var meshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
            var mesh = Object.Instantiate (meshRenderer.sharedMesh);

            mesh.subMeshCount = triangles.Count; 
            for (var i = 0; i < triangles.Count; i++) {
                mesh.SetTriangles (triangles [i], i);
            }
            AssetDatabase.CreateAsset (mesh, Path.Combine (dirname, name + ".asset"));
            meshRenderer.sharedMesh = mesh;

            return meshRenderer;
        }

        private static Plane CreatePlane (MeshFilter meshFilter)
        {
            var matrix = meshFilter.transform.localToWorldMatrix;
            var mesh = meshFilter.sharedMesh;
            var vertices = mesh.triangles.Take (3).Select (n => matrix.MultiplyPoint (mesh.vertices [n])).ToArray ();
            return new Plane (vertices [0], vertices [1], vertices [2]);
        }
    }
}
