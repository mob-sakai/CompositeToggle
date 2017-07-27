using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Mobcast.Coffee.Toggles;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO;

namespace Mobcast.Coffee.Toggles
{
	public static class PropertyEditor
	{
		public static bool abbreviation { get; set; }

		/// <summary>
		/// Reguler expression for find method path from method id.
		/// "UnityEngine.UI.Text, UnityEngine;System.Boolean, mscorlib;set_enable" is grouped as following:
		///   $1: UnityEngine.UI.
		///   $2: Text
		///   $3: System.
		///   $4: Boolean
		///   $5: set_enable
		/// </summary>
		static readonly Regex s_RegMethodId = new Regex(@"(\w+\.)*(\w+),.*;(\w+\.)*(\w+),.*;(\w+)", RegexOptions.Compiled);

		delegate object FieldDrawer(GUIContent label,object obj,Type type);

		readonly static Dictionary<Type, FieldDrawer> FieldDrawerMap = new Dictionary<Type, FieldDrawer>()
		{
			{ typeof(Vector2), (label, obj, type) => EditorGUILayout.Vector2Field(label, (Vector2)obj) },
			{ typeof(Vector3), (label, obj, type) => EditorGUILayout.Vector3Field(label, (Vector3)obj) },
			{ typeof(Vector4), (label, obj, type) => EditorGUILayout.Vector4Field(label.text, (Vector4)obj) },
			{ typeof(Color), (label, obj, type) => EditorGUILayout.ColorField(label, (Color)obj) },
			{ typeof(Rect), (label, obj, type) => EditorGUILayout.RectField(label, (Rect)obj) },
			{ typeof(ColorBlock), (label, obj, type) =>
				{
					EditorGUILayout.LabelField(label);
					EditorGUI.indentLevel++;
					ColorBlock block = (ColorBlock)obj;
					block.normalColor = EditorGUILayout.ColorField("Normal Color", block.normalColor);
					block.highlightedColor = EditorGUILayout.ColorField("Highlighted Color", block.highlightedColor);
					block.pressedColor = EditorGUILayout.ColorField("Pressed Color", block.pressedColor);
					block.disabledColor = EditorGUILayout.ColorField("Disabled Color", block.disabledColor);

					block.colorMultiplier = EditorGUILayout.Slider("Color Multiplier", block.colorMultiplier, 1, 5);
					block.fadeDuration = EditorGUILayout.Slider("Fade Duration", block.fadeDuration, 0, 5);
					EditorGUI.indentLevel--;
					return block;
				}
			},
			{ typeof(Bounds), (label, obj, type) => EditorGUILayout.BoundsField((Bounds)obj) },
			{ typeof(AnimationCurve), (label, obj, type) => EditorGUILayout.CurveField(label, (AnimationCurve)obj) },
			{ typeof(bool), (label, obj, type) => EditorGUILayout.Toggle(label, (bool)obj) },
			{ typeof(int), (label, obj, type) => EditorGUILayout.IntField(label, (int)obj) },
			{ typeof(long), (label, obj, type) => EditorGUILayout.LongField(label, (long)obj) },
			{ typeof(float), (label, obj, type) => EditorGUILayout.FloatField(label, (float)obj) },
			{ typeof(double), (label, obj, type) => EditorGUILayout.DoubleField(label, (double)obj) },
			{ typeof(string), (label, obj, type) =>
				{
					EditorGUILayout.PrefixLabel(label, EditorStyles.textField);
					GUILayout.Space(EditorGUIUtility.wideMode ? -20 : 0);
					using (new EditorGUILayout.HorizontalScope())
					{
						GUILayout.Space(EditorGUIUtility.wideMode ? EditorGUIUtility.labelWidth : 20);
						return EditorGUILayout.TextArea((string)obj);
					}
				}
			},
			{ typeof(LayerMask), (label, obj, type) => (LayerMask)EditorGUILayout.LayerField(label, (LayerMask)obj) },
			{ typeof(UnityEngine.Object), (label, obj, type) =>
				EditorGUILayout.ObjectField(label, (UnityEngine.Object)obj, type, true,
					GUILayout.MaxHeight(
						EditorGUIUtility.HasObjectThumbnail(type)
						? 48
						: EditorGUIUtility.singleLineHeight
					)
				)
			},
			{ typeof(Enum), (label, obj, type) => 
				Convert.ToInt32(type.IsDefined(typeof(FlagsAttribute), true)
					? EditorGUILayout.EnumMaskField(label, (Enum)Enum.ToObject(type, obj))
					: EditorGUILayout.EnumPopup(label, (Enum)Enum.ToObject(type, obj)))
			},
			{ typeof(object), (label, obj, type) =>
				{
					EditorGUILayout.PrefixLabel(label);
					return obj;
				}
			},
		};

		static readonly Dictionary<string, GUIContent> s_ContentMap = new Dictionary<string, GUIContent>();

		public static System.Action onPropertyChanged = ()=>{};

		static List<Component> s_TempComponents = new List<Component>();

		/// <summary>
		/// Draw property field.
		/// Return true if the value changed.
		/// </summary>
		/// <param name="property">Property.</param>
		/// <param name="index">Index.</param>
		/// <param name="enable">If set to <c>true</c> enable.</param>
		public static bool DrawPropertyField(Property property, int index, bool enable)
		{
			// Find label for the property.
			if (!s_ContentMap.ContainsKey(property.methodId))
				s_ContentMap[property.methodId] = new GUIContent(GetMethodPath(property, '.', true));

			GUIContent label = abbreviation
				? new GUIContent(Path.GetExtension(s_ContentMap[property.methodId].text).Substring(1))
				: s_ContentMap[property.methodId];

			// If the property is invalid, disable the property.
			if (property.methodInfo == null || property.parameterList == null || property.parameterList.count <= index)
			{
				DrawField(label, null, null, false);
				return false;
			}

			// Draw the property.
			EditorGUI.BeginChangeCheck();
			object newValue = DrawField(label, property.parameterList.GetObject(index), property.parameterType, enable);
			if (EditorGUI.EndChangeCheck())
			{
				property.parameterList.SetObject(index, newValue);
				return true;
			}
			else
				return false;
		}


		/// <summary>
		/// Draws the field.
		/// </summary>
		/// <param name="label">Label.</param>
		/// <param name="obj">Object.</param>
		/// <param name="type">Type.</param>
		/// <param name="enable">If set to <c>true</c> enable.</param>
		static object DrawField(GUIContent label, object obj, Type type, bool enable)
		{
			Color defaultColor = EditorStyles.label.normal.textColor;
			EditorStyles.label.normal.textColor = enable ? defaultColor : new Color(1,0.35f,0);
			bool drawnField = false;

			if (type != null)
			{
				// Find drawer for argument.
				foreach (var pair in FieldDrawerMap)
				{
					if (pair.Key.IsAssignableFrom(type))
					{
						obj = FieldDrawerMap[pair.Key](label, obj, type);
						drawnField = true;
						break;
					}
				}
			}

			// Not found drawer. draw only label.
			if (!drawnField)
			{
				EditorStyles.label.normal.textColor = Color.red;
				EditorGUILayout.LabelField(label);
			}

			EditorStyles.label.normal.textColor = defaultColor;
			return obj;
		}


		/// <summary>
		/// Creates the target menu.
		/// </summary>
		public static GenericMenu CreateTargetMenu(Component current, List<Property> activedTargets, int count)
		{
			IEnumerable<Type> availableTypes;
			if (current)
			{
				current.GetComponents(s_TempComponents);
				availableTypes = s_TempComponents
					.Where(x => x != current)
					.Select(x => x.GetType());
			}
			else
			{
				availableTypes = activedTargets
					.Where(x => x.methodInfo != null)
					.Select(x => x.methodInfo.ReflectedType);
			}
			return CreateTargetMenu(current, activedTargets, availableTypes, count);
		}

		/// <summary>
		/// Creates the target menu.
		/// </summary>
		public static GenericMenu CreateTargetMenu(Component current, List<Property> activedTargets, IEnumerable<Type> availableTypes, int count)
		{
			GenericMenu menu = new GenericMenu();

			// Append all activated properties. When clicked, switch activation.
			menu.AddDisabledItem(new GUIContent("Activated Toggle Targets"));
			foreach (var property in activedTargets)
			{
				string id = property.methodId;
				menu.AddItem(
					new GUIContent(GetMethodPath(property, '.')),
					true,
					()=>DeactivatePropetyTarget(id,activedTargets)
				);
			}

			menu.AddSeparator("");
			menu.AddDisabledItem(new GUIContent("Available Toggle Targets"));
			foreach (Type type in availableTypes.Distinct())
			{
				AppendAvailableTargetToMenu(current, activedTargets, count, menu, type);
			}

			return menu;
		}


		/// <summary>
		/// Appends the all available target contained in this type, to menu.
		/// The target is a methods have one argument, like a setter method.
		/// </summary>
		/// <param name="menu">Menu.</param>
		/// <param name="type">Type.</param>
		static void AppendAvailableTargetToMenu(Component current, List<Property> activedTargets, int count, GenericMenu menu, Type type)
		{
			// Collect toggle targets and headers for menu.
			Type argumentType, argumentArrayType;
			List<object> list = new List<object>();

			HashSet<Type> ignoreDeclaringType = new HashSet<Type>(){ typeof(UnityEngine.Object), typeof(Component), typeof(GameObject) };

			// Add available properties.
			list.Add(string.Format("{0}/Properties", type.Name));
			list.AddRange(
				type.GetProperties()
				.Where(pi => pi.CanWrite && !pi.IsDefined(typeof(ObsoleteAttribute), true))
				.Select(pi => pi.GetSetMethod())
				.Where(mi => mi != null && mi.ReflectedType == type && !ignoreDeclaringType.Contains(mi.DeclaringType) && Property.TryGetParameterListType(mi, out argumentType, out argumentArrayType))
				.OrderBy(mi => GetInheritanceDepth(mi.DeclaringType))
				.OfType<object>()
			);

			// Add available method targets.
			list.Add(string.Format("{0}/", type.Name));
			list.Add(string.Format("{0}/Methods", type.Name));
			list.AddRange(
				type.GetMethods()
				.Where(mi => (mi.ReturnType == typeof(void)) && !mi.IsSpecialName && !mi.IsDefined(typeof(ObsoleteAttribute), true))
				.Where(mi => mi != null && mi.ReflectedType == type && !ignoreDeclaringType.Contains(mi.DeclaringType) && Property.TryGetParameterListType(mi, out argumentType, out argumentArrayType))
				.OrderBy(mi => GetInheritanceDepth(mi.DeclaringType))
				.OfType<object>()
			);

			// Add menu to select available targets.
			HashSet<int> infos = new HashSet<int>(activedTargets.Where(x => x.methodInfo != null).Select(x => x.methodInfo.GetHashCode()));
			foreach (var item in list)
			{
				// Add headder menu.
				if (item is string)
				{
					menu.AddDisabledItem(new GUIContent((string)item));
				}
				// Add target.
				else if (item is MethodInfo)
				{
					// Add menu to check/uncheck property or method.
					MethodInfo mi = item as MethodInfo;
					bool isActive = infos.Contains(mi.GetHashCode());
					menu.AddItem(new GUIContent(GetMethodPath(type, mi, '/')),
						isActive,
						() => {
						string id = ConvertToMethodIdentifier(mi, type);
						if(isActive)
							DeactivatePropetyTarget(id, activedTargets);
						else
							ActivatePropetyTarget(current, activedTargets, mi, id, count);
					}
//						isActive
//							? () => DeactivatePropetyTarget(ConvertToMethodIdentifier(mi, type), activedTargets)
//							: () => SwitchActivePropetyTarget(current, activedTargets, mi, type, count)
					);
				}
			}
		}


		public static void DeactivatePropetyTarget(string methodId, List<Property> activedTargets)
		{
			// The target is active now and switch to be deactive.
			// Remove target from toggle.
			int index = activedTargets.FindIndex(x => x.methodId == methodId);
			if (0 <= index)
			{
				activedTargets.RemoveAt(index);
				onPropertyChanged();
			}
		}

		/// <summary>
		/// Switchs the target.
		/// </summary>
		/// <param name="info">Info.</param>
		public static void ActivatePropetyTarget(Component current, List<Property> activedTargets, MethodInfo info, string id, int count)
		{
			// 
			Type argumentType, argumentArrayType;
			if (!Property.TryGetParameterListType(info, out argumentType, out argumentArrayType))
				return;

			// The target is deactive now and switch to be active.
			// Add target to toggle, with its arguments.
			if (Property.TryGetParameterListType(argumentType, out argumentArrayType))
			{
				IParameterList args = (argumentArrayType != typeof(ObjectParameterList)) ? (IParameterList)JsonUtility.FromJson("{}", argumentArrayType) : new ObjectParameterList();
				args.FitSize(count);

				if (current)
					FillArgumentsByGetter(current, info, args);

				activedTargets.Add(new Property(id, args));

				// Sorting
				activedTargets.Sort((x,y)=>x.methodId.CompareTo(y.methodId));
			}

			if (current)
				UnityEditor.EditorUtility.SetDirty(current);
			
			onPropertyChanged();

		}

		public static string ConvertToMethodIdentifier(MethodInfo info, Type type)
		{
			// The identifier is structured as follows: "ReflectedTypeName:ArgumentTypeName:MethodName"
			return string.Format(Property.ID_FORMAT, GetShorAssemblyQualifiedName(type), GetShorAssemblyQualifiedName(info.GetParameters()[0].ParameterType), info.Name);
		}

		public static MethodInfo ConvertToMethodInfo(string id)
		{
			var names = id.Split(Property.ID_SEPERATOR);
			return Type.GetType(names[0]).GetMethod(names[2], new Type[]{Type.GetType(names[1])});
		}

		/// <summary>
		/// Gets the name of the shor assembly qualified.
		/// </summary>
		/// <param name="type">Type.</param>
		static string GetShorAssemblyQualifiedName(Type type)
		{
			string name = type.AssemblyQualifiedName;
			int length = name.IndexOf(", Version");
			return length < 0 ? name : name.Substring(0, length);
		}

		/// <summary>
		/// Fills the arguments by getter.
		/// </summary>
		/// <param name="current">Current.</param>
		/// <param name="info">Info.</param>
		/// <param name="args">Arguments.</param>
		public static void FillArgumentsByGetter(Component current, MethodInfo info, IParameterList args, int index = -1)
		{
			if (current == null || info == null || !info.IsSpecialName || args == null)
				return;

			object defaultValue;
			var getMethodInfo = info.ReflectedType.GetMethod("get_" + info.Name.Substring(4));
			Component component = current.GetComponent(info.ReflectedType);
			if (getMethodInfo != null && component)
			{
				defaultValue = getMethodInfo.Invoke(current.GetComponent(getMethodInfo.ReflectedType), new object[0]);

				for (int i = 0; i < args.count; i++)
				{
					if (0 <= index && index != i)
						continue;
							
					GUI.changed = GUI.changed || !object.Equals(args.GetObject(i), defaultValue);
					args.SetObject(i, defaultValue);
				}
			}
		}

		/// <summary>
		/// Gets the inheritance depth.
		/// </summary>
		/// <param name="type">Type.</param>
		public static int GetInheritanceDepth(Type type)
		{
			return (type.BaseType != null) ? GetInheritanceDepth(type.BaseType) + 1 : 0;
		}

		/// <summary>
		/// Gets the method path.
		/// </summary>
		/// <returns>The method path.</returns>
		/// <param name="p">P.</param>
		/// <param name="seperator">Seperator.</param>
		/// <param name="withoutArgument">If set to <c>true</c> without argument.</param>
		public static string GetMethodPath(Property p, char seperator, bool withoutArgument = false)
		{
			return  p.methodInfo != null ? GetMethodPath(p.methodTargetType ,p.methodInfo, seperator, withoutArgument) : s_RegMethodId.Replace(p.methodId, "$2.$4 ($5)");
		}

		/// <summary>
		/// Gets the method path.
		/// </summary>
		/// <returns>The method path.</returns>
		/// <param name="methodInfo">Method info.</param>
		/// <param name="seperator">Seperator.</param>
		/// <param name="withoutArgument">If set to <c>true</c> without argument.</param>
		public static string GetMethodPath(Type type, MethodInfo methodInfo, char seperator, bool withoutArgument = false)
		{
			string format = withoutArgument ? "{0}{1}{2}" : "{0}{1}{2} ({3})";
			string argumentTypeName = withoutArgument ? "" : GetTypeAliasName(methodInfo.GetParameters()[0].ParameterType);

			return string.Format(
				format,
				type.Name,
				seperator,
				methodInfo.IsSpecialName ? methodInfo.Name.Substring(4) : methodInfo.Name,
				argumentTypeName
			);
		}

		/// <summary>
		/// Gets the method path.
		/// </summary>
		/// <returns>The method path.</returns>
		/// <param name="p">P.</param>
		/// <param name="seperator">Seperator.</param>
		/// <param name="withoutArgument">If set to <c>true</c> without argument.</param>
		public static string GetMethodPath(string methodId)
		{
			return s_RegMethodId.Replace(methodId, "$2.$4 ($5)");
		}

		/// <summary>
		/// Gets the name of the type alias.
		/// </summary>
		/// <param name="type">Type.</param>
		static string GetTypeAliasName(Type type)
		{
			if (type.IsEnum)
				return type.Name;

			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Int32:
					return "int";
				case TypeCode.Int64:
					return "long";
				case TypeCode.Single:
					return "float";
				case TypeCode.Double:
					return "double";
				case TypeCode.Boolean:
					return "bool";
				case TypeCode.String:
					return "string";
				default:
					return type.Name;
			}
		}

		/// <summary>
		/// Bakes the property.
		/// </summary>
		/// <param name="properties">Properties.</param>
		public static void Bake(IEnumerable<Property> properties)
		{
			var path = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("__Genarated__BakedProperty t:MonoScript")[0]);
			string text = System.IO.File.ReadAllText(path);

			// Bake properties if it needed.
			bool dirty = false;
			foreach (var p in properties)
				dirty = Bake(p, ref text) | dirty;

			// When any property has baked, update script.
			if (dirty)
			{
				System.IO.File.WriteAllText(path, text);
				AssetDatabase.ImportAsset(path);
			}
		}

		/// <summary>
		/// Bakes the property.
		/// </summary>
		/// <returns><c>true</c>, if property target was baked, <c>false</c> otherwise.</returns>
		/// <param name="property">Property.</param>
		/// <param name="text">Text.</param>
		public static bool Bake(Property property, ref string text)
		{
			if (property == null || property.methodInfo == null)
				return false;

			Type arrayType;
			if (!Property.TryGetParameterListType(property.parameterType, out arrayType))
				return false;

			string id = string.Format("\"{0}\"", property.methodId);
			if (text.Contains(id))
				return false;

			MethodInfo mi = property.methodInfo;
			string content = string.Format("{{ {3}, (target, args, index) => (target as {0}).{1}{5}(({2})(args as {4}).GetRaw (index)) }},",
								property.methodTargetType.FullName,
				                 mi.IsSpecialName ? mi.Name.Substring(4) : mi.Name,
				                 property.parameterType.FullName,
				                 id,
				                 arrayType.FullName,
				                 mi.IsSpecialName ? " = " : ""
			                 );
			text = new Regex("(\t+)(// BAKED PROPERTIES START)").Replace(text, "$0\n$1" + content);

			return true;

		}
	}
}