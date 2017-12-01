using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Text;
using Mobcast.Coffee.Toggles;


namespace Mobcast.Coffee.Toggles
{
	using ValueType = CompositeToggle.ValueType;

	/// <summary>
	/// 複合トグルエディタ.
	/// </summary>
	[CustomEditor(typeof(CompositeToggle), true)]
	[CanEditMultipleObjects]
	public class CompositeToggleEditor : Editor
	{
		//---- ▼ GUIキャッシュ ▼ ----
		static GUIContent contentHierarchy;
		static GUIContent contentPlus;
		static GUIContent contentMinus;
		static GUIContent contentAction;
		static GUIContent contentGroup;
		static GUIContent contentActivation;
		static GUIContent contentOnValueChanged;
		static GUIStyle styleTitle;
		protected static GUIStyle styleHeader;
		protected static GUIStyle styleInner;
		protected static GUIStyle styleComment;


		static bool cached;

//		static bool rec = false;


		protected void CacheGUI()
		{
			if (cached)
				return;
			cached = true;

			contentPlus = new GUIContent(string.Empty, EditorGUIUtility.FindTexture("Toolbar Plus"), "Add Element");
			contentMinus = new GUIContent(string.Empty, EditorGUIUtility.FindTexture("Toolbar Minus"), "Remove Element");
			contentHierarchy = new GUIContent(EditorGUIUtility.FindTexture("unityeditor.hierarchywindow"), "Auto-Construction based on children");

			contentAction = new GUIContent("Invoke Action");
			contentGroup = new GUIContent("Grouping Toggle");
			contentActivation = new GUIContent("Activate GameObject");
			contentOnValueChanged = new GUIContent("OnValueChanged (Persistent)");


			styleTitle = new GUIStyle("GUIEditor.BreadcrumbLeft");
			styleTitle.fixedHeight = 17;
			styleTitle.contentOffset = new Vector2(12, 0);
			styleTitle.alignment = TextAnchor.UpperLeft;

			styleHeader = new GUIStyle("RL Header");
			styleHeader.alignment = TextAnchor.MiddleLeft;
			styleHeader.fontSize = 11;
			styleHeader.margin = new RectOffset(0, 0, 0, 0);
			styleHeader.padding = new RectOffset(8, 8, 0, 0);
			styleHeader.normal.textColor = EditorStyles.label.normal.textColor;

			styleInner = new GUIStyle("RL Background");
			styleInner.margin = new RectOffset(0, 0, 0, 0);
			styleInner.padding = new RectOffset(4, 4, 3, 6);

			styleComment = new GUIStyle("ProfilerBadge");
			styleComment.fixedHeight = 0;
			styleComment.contentOffset += new Vector2(5,0);
			styleComment.fontSize = 11;
		}

		//---- ▲ GUIキャッシュ ▲ ----
		ReorderableList roSyncToggles;
		CompositeToggle current;

		//		MethodInfo miTransformParentChanged;

		SerializedProperty spComments;
		SerializedProperty spActions;
		SerializedProperty spGroupToggles;
		SerializedProperty spActivations;
		SerializedProperty spOnValueChanged;

		SerializedProperty spValueType;
		SerializedProperty spIgnoreParentToggle;
		SerializedProperty spResetValueOnAwake;
		SerializedProperty spSyncedToggles;

		static CompositeToggle[] allToggles;
		static List<CompositeToggle> syncedByOtherToggles = new List<CompositeToggle>();
		static HashSet<CompositeToggle> multipleRelations;

		protected void OnEnable()
		{
//			rec = false;
			RefleshAllToggles();
			
			roSyncToggles = new ReorderableList(serializedObject, serializedObject.FindProperty("m_SyncedToggles"), false, false, true, true);
			roSyncToggles.drawElementCallback = (rect, index, isActive, isFocus) =>
			{
				var sp = roSyncToggles.serializedProperty.GetArrayElementAtIndex(index);
				EditorGUI.PropertyField(rect, sp, GetLabelWithWarning("Notifiy Target", sp.objectReferenceValue as CompositeToggle));
			};
			roSyncToggles.elementHeight = 16;
			roSyncToggles.headerHeight = 0;

			current = target as CompositeToggle;
			current.Reflesh();

			spComments = serializedObject.FindProperty("m_Comments");
			spActions = serializedObject.FindProperty("m_Actions");
			spGroupToggles = serializedObject.FindProperty("m_GroupedToggles");
			spActivations = serializedObject.FindProperty("m_ActivateObjects");
			spOnValueChanged = serializedObject.FindProperty("m_OnValueChanged");

			spValueType = serializedObject.FindProperty("m_ValueType");
			spIgnoreParentToggle = serializedObject.FindProperty("m_IgnoreParent");
			spResetValueOnAwake = serializedObject.FindProperty("m_ResetValueOnAwake");
			spSyncedToggles = serializedObject.FindProperty("m_SyncedToggles");

			s_AvailableReflectedTypes = new HashSet<Type>(current.GetComponents<Component>().Where(x => x != null).Select(x => x.GetType()));
			s_AvailableReflectedTypes.Add(typeof(GameObject));

			syncedByOtherToggles.Clear();
			foreach (var toggle in Resources.FindObjectsOfTypeAll<CompositeToggle>())
			{
				if (toggle == current)
					continue;
				
				var sp = new SerializedObject(toggle).FindProperty("m_SyncedToggles");
				if (Enumerable.Range(0, sp.arraySize).Any(i => sp.GetArrayElementAtIndex(i).objectReferenceValue == current))
					syncedByOtherToggles.Add(toggle);
			}

			UpdateMultipleRelations();
		}

		protected void OnDisable()
		{
//			rec = false;
		}


		void UpdateMultipleRelations()
		{
			multipleRelations = new HashSet<CompositeToggle>(
				current.children
				.Concat(new []{current.parent})
				.Concat(current.groupedToggles)
				.Concat(current.syncedToggles)
//				.Concat(Enumerable.Range(0,spGroupToggles.arraySize).Select(i=>spGroupToggles.GetArrayElementAtIndex(i).objectReferenceValue as CompositeToggle))
//				.Concat(Enumerable.Range(0,spSyncedToggles.arraySize).Select(i=>spSyncedToggles.GetArrayElementAtIndex(i).objectReferenceValue as CompositeToggle))
				.Where(x => x)
				.GroupBy(x => x)
				.Where(x => 1 < x.Count())
				.Select(g => g.FirstOrDefault()));
		}


		/// <summary>
		/// インスペクタGUIコールバック.
		/// Inspectorウィンドウを表示するときにコールされます.
		/// </summary>
		public override void OnInspectorGUI()
		{
			CacheGUI();
			serializedObject.Update();


			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(-13);
			EditorGUILayout.BeginVertical();

			// Draw toggle summary(value type, ignore in parent, sync).
			OnDrawSummary();

			// Draw toggle relations(parent/children, synced, grouped by).
			OnDrawRelation();

			//トグルを全件表示します.
			OnDrawToggles();

			//コールバック
			if (current.onValueChanged.GetPersistentEventCount() != 0)
				EditorGUILayout.PropertyField(spOnValueChanged);

			EditorGUILayout.EndVertical();
			GUILayout.Space(-4);
			EditorGUILayout.EndHorizontal();

			serializedObject.ApplyModifiedProperties();
		}

		/// <summary>
		/// 概要を表示します.
		/// </summary>
		protected void OnDrawSummary()
		{
			serializedObject.Update();

			//グループに所属している場合、パラメータを規定のものに変更.
			int indexInGroup = current.indexInGroup;
			if (0 <= indexInGroup)
			{
				spValueType.intValue = (int)ValueType.Boolean;
				spIgnoreParentToggle.boolValue = true;
				spResetValueOnAwake.boolValue = false;
				spSyncedToggles.ClearArray();

				if (serializedObject.ApplyModifiedProperties())
					current.OnTransformParentChanged();
				return;
			}

			using (new EditorGUILayout.VerticalScope("box"))
			{
				//トグルタイプポップアップを描画します.
				EditorGUILayout.PropertyField(spValueType);
				EditorGUILayout.PropertyField(spIgnoreParentToggle);
				EditorGUILayout.PropertyField(spResetValueOnAwake);

				//自分の値が変更されたときに通知するトグルリストを描画します.
				bool hasSyncToggles = 0 < roSyncToggles.count;
				if (EditorGUILayout.Toggle("Sync Other Toggles", hasSyncToggles) != hasSyncToggles)
				{
					hasSyncToggles = !hasSyncToggles;
					if (hasSyncToggles)
						roSyncToggles.serializedProperty.InsertArrayElementAtIndex(0);
					else
						roSyncToggles.serializedProperty.ClearArray();
				}

				if (hasSyncToggles)
				{
					GUILayout.Space(-2);
					roSyncToggles.DoLayoutList();
				}
			}

			if (serializedObject.ApplyModifiedProperties())
			{
				current.OnTransformParentChanged();
				current.Reflesh();
				ResetToggleValue();
				serializedObject.Update();

				UpdateMultipleRelations();
			}
		}

		static StringBuilder s_StringBuilder = new StringBuilder();

		GUIContent GetLabelWithWarning(string label, CompositeToggle otherToggle)
		{
			if (!otherToggle)
				return new GUIContent(label);
			
			s_StringBuilder.Length = 0;

			// Self-reference.
			if (current == otherToggle)
				s_StringBuilder.AppendFormat("- You can not specify the toggle in itself.\n");

			// Conflict reference.
			if (multipleRelations.Contains(otherToggle))
				s_StringBuilder.AppendFormat("- The toggle can not have multiple relations.\n");

			// Value type or toggole count is mismatched.
			if (!current.groupedToggles.Contains(otherToggle))
			{
				bool isIndexedBoth = (current.valueType <= ValueType.Index && otherToggle.valueType <= ValueType.Index);
				if (!isIndexedBoth && current.valueType != otherToggle.valueType)
					s_StringBuilder.AppendFormat("- Value type is mismatched: {0}\n", otherToggle.valueType);

				if (current.count != otherToggle.count)
					s_StringBuilder.AppendFormat("- Toggle count is mismatched: {0}\n", otherToggle.count);
			}

			if (0 < s_StringBuilder.Length)
			{
				s_StringBuilder.Length--;
				return new GUIContent(label, EditorGUIUtility.FindTexture("console.warnicon.sml"), s_StringBuilder.ToString());
			}
			else
				return new GUIContent(label);
		}


		/// <summary>
		/// トグル関係を表示します.
		/// </summary>
		protected void OnDrawRelation()
		{

			int indexInGroup = current.indexInGroup;
//			Debug.LogFormat("indexInGroup:{0}, children:{1}, syncedByOtherToggles:{2}", indexInGroup, current.children.Count, syncedByOtherToggles.Count);
			if (indexInGroup < 0 && current.parent == null && current.children.Count == 0 && syncedByOtherToggles.Count == 0)
				return;

			using (new EditorGUI.DisabledGroupScope(true))
			using (new EditorGUILayout.VerticalScope("box"))
			{
				//リレーション一覧
				if (current.parent)
				{
					EditorGUILayout.ObjectField(GetLabelWithWarning("Parent", current.parent), current.parent, typeof(CompositeToggle), true);
				}
				else if (0 <= indexInGroup)
				{
					EditorGUILayout.ObjectField(new GUIContent(GetIndexedLabel(current.groupParent.valueType, indexInGroup, true)), current.groupParent, typeof(CompositeToggle), true);
				}

				foreach (var toggle in current.children)
				{
					EditorGUILayout.ObjectField(GetLabelWithWarning("Child", toggle), toggle, typeof(CompositeToggle), true);
				}

				foreach (var toggle in syncedByOtherToggles)
				{
					EditorGUILayout.ObjectField(GetLabelWithWarning("Synced By", toggle), toggle, typeof(CompositeToggle), true);
				}
			}
		}


		/// <summary>
		/// トグルを全件表示します.
		/// アクションモードではトグルアクションを、グループモードではトグルグループを描画します.
		/// </summary>
		protected void OnDrawToggles()
		{
			// Draw toolbar.
			DrawToggleToolbar();

			//トグルを全件表示.
			EditorGUILayout.BeginVertical(styleInner, GUILayout.MinHeight(1f));
			{
				//アクション内容を全件表示.
				bool drawTarget = current.toggleProperties.Count != 0 || spActions.arraySize != 0 || spGroupToggles.arraySize != 0 || spActivations.arraySize != 0;
				for (int i = 0; i < current.count; i++)
				{
					//エレメントタイトル.
					DrawElementTitle(i);

					if (drawTarget)
						DrawTargetIndex(i);
				}
			}
			EditorGUILayout.EndVertical();

			// Draw footer 
			DrawToggleFooter();
		}


		/// <summary>
		/// Draws the style sheet toolbar.
		/// </summary>
		/// <param name="behavior">Target behavior.</param>
		/// <param name="styleSheetAsset">Style sheet asset.</param>
		void DrawToggleToolbar()
		{
			bool changed = GUI.changed;

			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

			// Save style property from target behavior.
			using (var cc = new EditorGUI.DisabledGroupScope(!current || ValueType.Index < current.valueType))
			{
				if (GUILayout.Button(new GUIContent("Save"), EditorStyles.toolbarButton))
				{
					foreach (var property in current.toggleProperties)
					{
						property.parameterList.FitSize(current.count);
						PropertyEditor.FillArgumentsByGetter(current, property.methodInfo, property.parameterList, current.indexValue);
					}
				}

				if (GUILayout.Button(new GUIContent("Load"), EditorStyles.toolbarButton))
				{
					ResetToggleValue();
				}
			}

			GUILayout.Space(8);

			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Bake", EditorStyles.toolbarButton))
			{
				PropertyEditor.Bake(current.toggleProperties);
			}

			if (GUILayout.Button("Add Property", EditorStyles.toolbarPopup))
			{
				var menu = PropertyEditor.CreateTargetMenu(current, current.toggleProperties, current.count);
				AppendAvailableOptionToMenu(menu).ShowAsContext();
			}


			//自動構築ボタン.
			EditorGUI.BeginDisabledGroup(spGroupToggles.arraySize == 0 && spActivations.arraySize == 0);
			if (GUILayout.Button(contentHierarchy, EditorStyles.toolbarButton))
			{
				int count = current.count;
				var sb = new StringBuilder("Are you sure you want to reconstruct toggles based on child transforms as following?\n");
				var transform = current.transform;
				var children = Enumerable.Range(0, transform.childCount)
					.Select(i => transform.GetChild(i))
					.ToList();

				var childToggles = children
					.Select(trans => trans.GetComponent<CompositeToggle>())
					.Where(toggle => toggle != null)
					.ToList();

				if (0 < spActivations.arraySize)
				{
					count = Mathf.Max(count, children.Count);
					sb.AppendFormat("\n{0} GameObjects for 'Set Activation' :\n{1}",
						children.Count,
						children.Aggregate(new StringBuilder(), (a, b) => a.AppendFormat(" - {0}\n", b.name))
					);
				}

				if (0 < spGroupToggles.arraySize)
				{
					count = Mathf.Max(count, childToggles.Count);
					sb.AppendFormat("\n{0} CompositeToggles for 'Toggle Grouping' :\n{1}",
						childToggles.Count,
						childToggles.Aggregate(new StringBuilder(), (a, b) => a.AppendFormat(" - {0}\n", b.name))
					);
				}

				if (EditorUtility.DisplayDialog("Auto-Construction Based on Children", sb.ToString(), "Yes", "No"))
				{
					current.count = count;
					serializedObject.Update();

					for (int i = 0; i < Mathf.Min(spGroupToggles.arraySize, childToggles.Count); i++)
						spGroupToggles.GetArrayElementAtIndex(i).objectReferenceValue = childToggles[i];

					for (int i = 0; i < Mathf.Min(spActivations.arraySize, children.Count); i++)
						spActivations.GetArrayElementAtIndex(i).objectReferenceValue = children[i].gameObject;
				}
			}
			EditorGUI.EndDisabledGroup();

			EditorGUILayout.EndHorizontal();
			GUI.changed = changed;
		}

		/// <summary>
		/// トグルの追加、削除、グループ自動構築メニューを含む、トグルフッターを描画します.
		/// </summary>
		void DrawToggleFooter()
		{
			if (current.valueType == ValueType.Boolean)
				return;

			using (new EditorGUILayout.HorizontalScope())
			{
				//フッター背景.
				GUILayout.FlexibleSpace();
				var rect = GUILayoutUtility.GetRect(58, 58, 20, 20);
				GUI.Label(rect, GUIContent.none, "RL Footer");

				//+ボタン.
				rect.y -= 3;
				rect.x += 5;
				rect.width = 20;
				if (GUI.Button(rect, contentPlus, EditorStyles.label))
				{
					current.count++;
					EditorUtility.SetDirty(current.gameObject);
					serializedObject.Update();
				}

				//-ボタン.
				rect.x += 25;
				using (new EditorGUI.DisabledGroupScope(current.count <= 2))
				{
					if (GUI.Button(rect, contentMinus, EditorStyles.label))
					{
						current.count--;
						EditorUtility.SetDirty(current.gameObject);
						serializedObject.Update();
					}
				}
			}
			GUILayout.Space(-5);
		}

		/// <summary>
		/// トグル項目のタイトルを描画します.
		/// 背景、トグルコントロール、トグルタイトルを1セットとして描画します.
		/// インスペクタ上でトグルコントロールを切り替えることで、トグル状態を変更できます.
		/// </summary>
		/// <param name="rect">描画矩形.</param>
		/// <param name="index">項目インデックス.</param>
		protected void DrawElementTitle(int index)
		{
			int val = 1 << index;
			bool isActive = 0 < (current.maskValue & val);

			//トグル名.
			Rect rect = GUILayoutUtility.GetRect(10, 17, styleTitle, GUILayout.ExpandWidth(true));
			float width = rect.width - 10;
			rect.x += 6;
			rect.y += 1;
			rect.width = 80;
			GUI.Label(rect, GetIndexedLabel(current.valueType, index, false), styleTitle);

			//トグル操作した場合、値を変更.
			rect.x -= 2;
			bool isRadio = current.valueType <= ValueType.Index;
			if (EditorGUI.Toggle(rect, isActive, isRadio ? EditorStyles.radioButton : EditorStyles.toggle) != isActive)
			{
				RefleshAllToggles();

				//valueTypeに応じて次のトグル状態に更新.
				switch (current.valueType)
				{
					case ValueType.Boolean:
					case ValueType.Index:
						current.indexValue = !isActive ? index : ((index + 1) % current.count);
						break;
					case ValueType.Count:
						current.countValue = !isActive ? index + 1 : index;
						break;
					case ValueType.Flag:
						current.maskValue = !isActive ? (current.maskValue | val) : (current.maskValue & ~val);
						break;
				}
			}

			//コメント
			rect.x += rect.width + 15;
			rect.width = width - rect.width - 10;
			rect.height -= 2;
			var spComment = spComments.GetArrayElementAtIndex(index);
			using (new EditorGUI.PropertyScope(rect, null, spComment))
			{
				EditorGUI.BeginChangeCheck();

				GUI.SetNextControlName("Comment_" + index);
				Color backgroundColor = GUI.backgroundColor;
				GUI.backgroundColor = isActive ? Color.white : new Color(1, 1, 1, 0.75f);
				string comment = EditorGUI.TextField(rect, spComment.stringValue, styleComment);

				if (string.IsNullOrEmpty(spComment.stringValue) && !spComment.hasMultipleDifferentValues && GUI.GetNameOfFocusedControl() != ("Comment_" + index))
				{
					GUI.backgroundColor = Color.clear;
					GUI.Label(rect, "Comment...", styleComment);
				}

				if (EditorGUI.EndChangeCheck())
				{
					spComment.stringValue = comment;
					spComment.serializedObject.ApplyModifiedProperties();
				}
				GUI.backgroundColor = backgroundColor;
			}
		}

		void RefleshAllToggles()
		{
			//非再生中の場合、CompositeToggle全てに対して親子関係を再計算.
			if (Application.isPlaying)
				return;
			
			foreach (var toggle in Resources.FindObjectsOfTypeAll<CompositeToggle>())
			{
				//　シーン上にないオブジェクトは除外されます.
				if (!toggle.gameObject || !toggle.gameObject.scene.IsValid())
					return;
				
				toggle.OnTransformParentChanged();
				toggle.Reflesh();
			}
		}

		void ResetToggleValue()
		{
			//valueTypeに応じて次のトグル状態に更新.
			current.forceNotifyNext = true;
			switch (current.valueType)
			{
				case ValueType.Boolean:
				case ValueType.Index:
					current.indexValue = current.indexValue;
					break;
				case ValueType.Count:
					current.countValue = current.countValue;
					break;
				case ValueType.Flag:
					current.maskValue = current.maskValue;
					break;
			}
		}

		public static string GetIndexedLabel(CompositeToggle.ValueType valueType, int index, bool inGroupSufix)
		{
			string format = inGroupSufix ? "'{0}' In Group" : "{0}";

			switch (valueType)
			{
				case CompositeToggle.ValueType.Boolean:
					return string.Format(format, index == 0 ? "False" : "True");
				case CompositeToggle.ValueType.Count:
					return string.Format(format, string.Format("Count {1}", valueType, index+1));
				default:
					return string.Format(format, string.Format("{0} {1}", valueType, index));
			}
		}


		static HashSet<Type> s_AvailableReflectedTypes = new HashSet<Type>();

		void DrawTargetIndex(int index)
		{
			GUILayout.Space(-4);
			EditorGUILayout.BeginVertical("helpbox");

			if (!serializedObject.isEditingMultipleObjects)
			{
				bool isIndex = current.valueType == ValueType.Boolean || current.valueType == ValueType.Index;
				EditorGUI.BeginDisabledGroup(!isIndex);
				// Draw each targets.
				foreach (var property in current.toggleProperties)
				{
					bool enable = s_AvailableReflectedTypes.Contains(property.methodInfo.ReflectedType);
					if (PropertyEditor.DrawPropertyField(property, index, enable))
					{
						if (0 != (current.maskValue & (1 << index)))
							ResetToggleValue();

						EditorUtility.SetDirty(current.gameObject);
						serializedObject.Update();
					}
				}
				EditorGUI.EndDisabledGroup();
			}
			else
			{
				EditorGUILayout.HelpBox("Multi-Properties editing is not supported.", MessageType.None);
			}

			// Draw external action.
			if (!spActions.hasMultipleDifferentValues && index < spActions.arraySize)
				EditorGUILayout.PropertyField(spActions.GetArrayElementAtIndex(index), contentAction);

			// Draw group toggle.
			if (!spGroupToggles.hasMultipleDifferentValues && index < spGroupToggles.arraySize)
			{
				var spToggle = spGroupToggles.GetArrayElementAtIndex(index);
				Rect r = EditorGUILayout.GetControlRect();
				using (new EditorGUI.PropertyScope(r, null, spToggle))
				{
					EditorGUI.BeginChangeCheck();

					EditorGUI.PropertyField(r, spToggle, GetLabelWithWarning("Grouping Toggle", spToggle.objectReferenceValue as CompositeToggle));
					if (EditorGUI.EndChangeCheck())
					{
						UpdateMultipleRelations();
					}
				}
			}

			// Draw activation.
			if (index < spActivations.arraySize)
				EditorGUILayout.PropertyField(spActivations.GetArrayElementAtIndex(index), contentActivation);

			EditorGUILayout.EndVertical();
		}

		GenericMenu AppendAvailableOptionToMenu(GenericMenu menu)
		{
			// Options.
			menu.AddSeparator("");
			menu.AddDisabledItem(new GUIContent("Options"));

			menu.AddItem(contentAction, 0 < spActions.arraySize, () => SwitchActivate(spActions));
			menu.AddItem(contentGroup, 0 < spGroupToggles.arraySize, () => SwitchActivate(spGroupToggles));
			menu.AddItem(contentActivation, 0 < spActivations.arraySize, () => SwitchActivate(spActivations));

			var ev = current.onValueChanged;
			menu.AddItem(contentOnValueChanged, ev.GetPersistentEventCount() != 0,
				() =>
				{
					if (ev.GetPersistentEventCount() != 0)
					{
						while (0 < ev.GetPersistentEventCount())
							UnityEditor.Events.UnityEventTools.RemovePersistentListener(ev, 0);
					}
					else
						UnityEditor.Events.UnityEventTools.AddPersistentListener(ev);
				}
			);
			return menu;
		}

		void SwitchActivate(SerializedProperty sp)
		{
			if (0 < sp.arraySize)
			{
				while (0 < sp.arraySize)
					sp.DeleteArrayElementAtIndex(0);
			}
			else
			{
				while (sp.arraySize < current.count)
					sp.InsertArrayElementAtIndex(sp.arraySize);
			}
			sp.serializedObject.ApplyModifiedProperties();
		}
	}
}
