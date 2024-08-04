using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace KiriMeshSplitter
{
	internal class MeshSplitterEditor : EditorWindow
	{
		[MenuItem ("MeshSplitter/MeshSplitter Editor")]
		private void Create ()
		{
			GetWindow<MeshSplitterEditor> ("MeshSplitter");
		}

		private static StyleLength SimulatedVerticalEditorGUILayoutSpaceLength() => new StyleLength(12.0f);
		private void CreateGUI()
		{
			var targetMeshField = new ObjectField("Mesh")
			{
				objectType = typeof(SkinnedMeshRenderer), allowSceneObjects = true,
				style =
				{
					marginBottom = SimulatedVerticalEditorGUILayoutSpaceLength()
				}
			};
			rootVisualElement.Add(targetMeshField);
			
			
			var quad = new ObjectField("Quad")
			{
				objectType = typeof(MeshFilter), allowSceneObjects = true,
				style =
				{
					marginBottom = SimulatedVerticalEditorGUILayoutSpaceLength()
				}
			};
			rootVisualElement.Add(quad);
			
			var splitByMaterials = new Button(() => { SplitByMaterials(targetMeshField.value as SkinnedMeshRenderer); })
				{
					style =
					{
						marginBottom = SimulatedVerticalEditorGUILayoutSpaceLength()
					}
				};
			rootVisualElement.Add(splitByMaterials);
			splitByMaterials.Add(new Label("split by materials"));
			splitByMaterials.style.display = targetMeshField.value != null ? DisplayStyle.Flex : DisplayStyle.None;
			
			var splitByQuad = new Button(() => { SplitByQuad(targetMeshField.value as SkinnedMeshRenderer, quad.value as MeshFilter); })
				{
					style =
					{
						marginBottom = SimulatedVerticalEditorGUILayoutSpaceLength()
					}
				};
			rootVisualElement.Add(splitByQuad);
			splitByQuad.Add(new Label("split by quad"));
			splitByQuad.style.display = quad.value != null ? DisplayStyle.Flex : DisplayStyle.None;
			
			targetMeshField.RegisterValueChangedCallback(ev =>
			{
				var visibleSplitByMaterial = ev.newValue != null;
				splitByMaterials.style.display = visibleSplitByMaterial ? DisplayStyle.Flex : DisplayStyle.None;
			});
			quad.RegisterValueChangedCallback(ev =>
			{
				if (ev.newValue != null && (ev.newValue as MeshFilter)!.name != "Quad")
				{
					quad.value = null;
				}
			});
			quad.RegisterValueChangedCallback(ev =>
			{
				var visibleSplitByQuad = ev.newValue != null;
				splitByQuad.style.display = visibleSplitByQuad ? DisplayStyle.Flex : DisplayStyle.None;
			});

		}

		private static void SplitByMaterials (SkinnedMeshRenderer meshRenderer)
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
			var triangles = new[] { meshRenderer.sharedMesh.GetTriangles (index) };

			var newMeshRenderer = CreateNewMesh (meshRenderer, triangles, submeshDir, meshName);
			newMeshRenderer.sharedMaterials = new[] { meshRenderer.sharedMaterials [index] };
		}

		private static GameObject CloneObject (GameObject gameObject)
		{
			return Instantiate (gameObject, gameObject.transform.parent);
		}

		private static void SplitByQuad (SkinnedMeshRenderer meshRenderer, MeshFilter meshFilter)
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

			for (var j = 0; j < mesh.subMeshCount; j++) {
				var triangles = mesh.GetTriangles (j);
				triA.Add (new List<int> ());
				triB.Add (new List<int> ());

				for (var i = 0; i < triangles.Length; i += 3) {
					var triangle = triangles.Skip (i).Take (3).ToList();
					var side = triangle.Any(n => plane.GetSide(matrix.MultiplyPoint(tris[n])));

					if (side) {
						triA [j].AddRange (triangle);
					} else {
						triB [j].AddRange (triangle);
					}

					if (i % 30 == 0) {
						EditorUtility.DisplayProgressBar (
							"処理中", 
							string.Format ("submesh:{0}/{1}, triangles:{2}/{3}", j, mesh.subMeshCount, i, triangles.Length),
							(float)i / triangles.Length
						);
					}
				}
			}

			CreateNewMesh (meshRenderer, triA.Select (n => n.ToArray ()).ToArray (), submeshDir, meshName + "_a");
			CreateNewMesh (meshRenderer, triB.Select (n => n.ToArray ()).ToArray (), submeshDir, meshName + "_b");
			meshRenderer.gameObject.SetActive (false);

			EditorUtility.ClearProgressBar (); 
		}

		private static SkinnedMeshRenderer CreateNewMesh (SkinnedMeshRenderer original, int[][] triangles, string dirname, string name)
		{
			var gameObject = CloneObject (original.gameObject);
			gameObject.name = name;
			var meshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
			var mesh = Instantiate (meshRenderer.sharedMesh);

			mesh.subMeshCount = triangles.Length; 
			for (var i = 0; i < triangles.Length; i++) {
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
