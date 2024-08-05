using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace KiriMeshSplitter
{
	public class MeshSplitterEditor : EditorWindow
	{
		[MenuItem ("MeshSplitter/MeshSplitter Editor")]
		private static void OnOpen()
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
			var targetMeshFieldProxy = new TypeSafeObjectField<SkinnedMeshRenderer>(targetMeshField);
			
			var quad = new ObjectField("Quad")
			{
				objectType = typeof(MeshFilter), allowSceneObjects = true,
				style =
				{
					marginBottom = SimulatedVerticalEditorGUILayoutSpaceLength()
				}
			};
			rootVisualElement.Add(quad);
			var quadFieldProxy = new TypeSafeObjectField<MeshFilter>(quad);

			// TODO: group them by Foldout
			// TODO: add bone weight threshold field (0.0f to 1.0f)
			var splitBoneByWeightField = new ObjectField("transform") { objectType = typeof(Transform) };
			rootVisualElement.Add(splitBoneByWeightField);
			var splitBoneByWeightFieldProxy = new TypeSafeObjectField<Transform>(splitBoneByWeightField);
			
			var splitByMaterials = new Button(() => { BusinessLogic.SplitByMaterials(targetMeshFieldProxy.Value); })
				{
					style =
					{
						marginBottom = SimulatedVerticalEditorGUILayoutSpaceLength()
					}
				};
			rootVisualElement.Add(splitByMaterials);
			splitByMaterials.Add(new Label("split by materials"));
			splitByMaterials.style.display = targetMeshFieldProxy.Value != null ? DisplayStyle.Flex : DisplayStyle.None;
			
			var splitByQuad = new Button(() =>
			{
				BusinessLogic.SplitByQuad(targetMeshFieldProxy.Value, quadFieldProxy.Value);
			})
				{
					style =
					{
						marginBottom = SimulatedVerticalEditorGUILayoutSpaceLength()
					}
				};
			rootVisualElement.Add(splitByQuad);
			splitByQuad.Add(new Label("split by quad"));
			splitByQuad.style.display = quadFieldProxy.Value != null ? DisplayStyle.Flex : DisplayStyle.None;

			var button = new Button(() =>
			{
				BusinessLogic.SplitByBoneWeight(targetMeshFieldProxy.Value, splitBoneByWeightFieldProxy.Value);
			});
			rootVisualElement.Add(button);
			button.Add(new Label("split by bone weight"));
			
			targetMeshFieldProxy.ObjectField.RegisterValueChangedCallback(ev =>
			{
				var visibleSplitByMaterial = ev.newValue != null;
				splitByMaterials.style.display = visibleSplitByMaterial ? DisplayStyle.Flex : DisplayStyle.None;
			});
			quad.RegisterValueChangedCallback(ev =>
			{
				if (ev.newValue != null && (ev.newValue as MeshFilter)!.name != "Quad")
				{
					quadFieldProxy.Value = null;
				}
			});
			quad.RegisterValueChangedCallback(ev =>
			{
				var visibleSplitByQuad = ev.newValue != null;
				splitByQuad.style.display = visibleSplitByQuad ? DisplayStyle.Flex : DisplayStyle.None;
			});
		}
	}
}
