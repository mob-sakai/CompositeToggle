using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Mobcast.Coffee.Toggles
{
	/// <summary>
	/// Property.
	/// </summary>
	[System.Serializable]
	public class Property : ISerializationCallbackReceiver
	{
		public const char ID_SEPERATOR = ';';
		public const string ID_FORMAT = "{0};{1};{2}";

		static readonly Dictionary<Type,Type> s_ParameterListTypeMap = new Dictionary<Type, Type>()
		{
			{ typeof(Object), typeof(ObjectParameterList) },
			{ typeof(int), typeof(IntParameterList) },
			{ typeof(bool), typeof(BoolParameterList) },
			{ typeof(long), typeof(LongParameterList) },
			{ typeof(float), typeof(FloatParameterList) },
			{ typeof(string), typeof(StringParameterList) },
			{ typeof(Color), typeof(ColorParameterList) },
			{ typeof(ColorBlock), typeof(ColorBlockParameterList) },
			{ typeof(Vector2), typeof(Vector2ParameterList) },
			{ typeof(Vector3), typeof(Vector3ParameterList) },
			{ typeof(Vector4), typeof(Vector4ParameterList) },
			{ typeof(Gradient), typeof(GradientParameterList) },
			{ typeof(AnimationCurve), typeof(AnimationCurveParameterList) },
			{ typeof(LayerMask), typeof(LayerParameterList) },
			{ typeof(Enum), typeof(IntParameterList) },
		};

		static readonly Type[] s_Types = new Type[1];

		/// <summary>
		/// Tries the type of the get parameter list.
		/// </summary>
		/// <returns><c>true</c>, if get parameter list type was tryed, <c>false</c> otherwise.</returns>
		/// <param name="argType">Argument type.</param>
		/// <param name="parameterListType">Parameter list type.</param>
		public static bool TryGetParameterListType(Type argType, out Type parameterListType)
		{
			parameterListType = null;
			if (s_ParameterListTypeMap.TryGetValue(argType, out parameterListType))
				return true;

			foreach (var pair in s_ParameterListTypeMap)
			{
				if (pair.Key.IsAssignableFrom(argType))
				{
					parameterListType = pair.Value;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Tries the type of the get parameter list.
		/// </summary>
		/// <returns><c>true</c>, if get parameter list type was tryed, <c>false</c> otherwise.</returns>
		/// <param name="mi">Mi.</param>
		/// <param name="argType">Argument type.</param>
		/// <param name="parameterListType">Parameter list type.</param>
		public static bool TryGetParameterListType(MethodInfo mi, out Type argType, out Type parameterListType)
		{
			ParameterInfo[] parameters = mi.GetParameters();
			if (parameters.Length == 1 && TryGetParameterListType(parameters[0].ParameterType, out parameterListType))
			{
				argType = parameters[0].ParameterType;
				return true;
			}
			else
			{
				parameterListType = null;
				argType = null;
				return false;
			}
		}

		/// <summary>
		/// Gets the method identifier.
		/// </summary>
		/// <value>The method identifier.</value>
		public string methodId
		{
			get
			{
				if (string.IsNullOrEmpty(m_MethodId))
					m_MethodId = string.Format(ID_FORMAT, m_MethodTargetType, m_ParameterType, m_MethodName);
				return m_MethodId;
			}
		}

		[SerializeField]
		string m_MethodId;

		BakedProperty.PropertySetter setter;

		[SerializeField]
		string m_MethodTargetType;

		[SerializeField]
		string m_ParameterType;

		[SerializeField]
		string m_MethodName;

		[SerializeField]
		string m_ParameterListJson;

		[SerializeField]
		ObjectParameterList m_ObjectParameterList;

		/// <summary>
		/// Gets the method info.
		/// </summary>
		/// <value>The method info.</value>
		public MethodInfo methodInfo
		{
			get
			{
				if (!m_MethodInfoChecked && m_MethodInfo == null && !hasParseError)
				{
					m_MethodInfoChecked = true;
					s_Types[0] = parameterType;
					m_MethodInfo = methodTargetType.GetMethod(m_MethodName, s_Types);
					s_Types[0] = null;
				}
				return m_MethodInfo;
			}
		}

		MethodInfo m_MethodInfo;

		/// <summary>
		/// Gets the type of the method target.
		/// </summary>
		/// <value>The type of the method target.</value>
		public Type methodTargetType { get; private set; }

		/// <summary>
		/// Gets the type of the parameter.
		/// </summary>
		/// <value>The type of the parameter.</value>
		public Type parameterType { get; private set; }

		/// <summary>
		/// Gets the arguments.
		/// </summary>
		/// <value>The arguments.</value>
		public IParameterList parameterList { get; private set; }

		static object[] s_TemporaryMethodArguments = new object[1];

		bool m_MethodInfoChecked = false;

		/// <summary>
		/// Gets a value indicating whether this <see cref="Mobcast.Coffee.Toggles.PropertyTarget"/> has parse error.
		/// </summary>
		/// <value><c>true</c> if has parse error; otherwise, <c>false</c>.</value>
		public bool hasParseError { get { return parameterList == null; } }

		public Property()
		{
		}

		public Property(string methodId, IParameterList args)
		{
			var names = methodId.Split(ID_SEPERATOR);
			m_MethodTargetType = names[0];
			m_ParameterType = names[1];
			m_MethodName = names[2];

			parameterList = args;
			m_ObjectParameterList = (args as ObjectParameterList);

			OnBeforeSerialize();
			OnAfterDeserialize();
		}

		/// <summary>
		/// Invoke the specified methodTarget and index.
		/// </summary>
		/// <param name="methodTarget">Method target.</param>
		/// <param name="index">Index.</param>
		public void Invoke(Object methodTarget, int index)
		{
			if (hasParseError || !methodTarget || parameterList.count <= index)
				return;

			try
			{
				// If the baked property setter exists, invoke it.
				if (setter != null)
				{
					setter(methodTarget, parameterList, index);
				}
				// If the method info exists, invoke it.
				else if (methodInfo != null)
				{
					s_TemporaryMethodArguments[0] = parameterList.GetObject(index);
					methodInfo.Invoke(methodTarget, s_TemporaryMethodArguments);
				}
				// Not invokable.
				else
				{
					UnityEngine.Debug.LogWarningFormat("Property '{0}' is not invokable.", methodId);
				}
			}
			catch (Exception e)
			{
				UnityEngine.Debug.LogErrorFormat("An exception has occurred during invocation of '{0}'.", methodId);
				UnityEngine.Debug.LogException(e);
			}
			finally
			{
				s_TemporaryMethodArguments[0] = null;
			}
		}


		#region ISerializationCallbackReceiver implementation

		public void OnBeforeSerialize()
		{
			if (!Application.isPlaying && parameterList != null)
				m_ParameterListJson = (parameterList is ObjectParameterList) ? "" : JsonUtility.ToJson(parameterList);

			if (string.IsNullOrEmpty(m_MethodId))
				m_MethodId = methodId;
		}

		public void OnAfterDeserialize()
		{
			m_MethodInfo = null;
			m_MethodInfoChecked = false;
			parameterList = null;
			parameterType = null;
			methodTargetType = null;

			// Find baked setter.
			BakedProperty.baked.TryGetValue(methodId, out setter);

			// Parse types from type name.
			methodTargetType = Type.GetType(m_MethodTargetType);
			parameterType = Type.GetType(m_ParameterType);

			// If all type parsings succeed, then parse the parameter list.
			Type parameterListType;
			if (methodTargetType != null && parameterType != null && TryGetParameterListType(parameterType, out parameterListType))
			{
				parameterList = (parameterListType == typeof(ObjectParameterList))
					? m_ObjectParameterList
					: (IParameterList)JsonUtility.FromJson(!string.IsNullOrEmpty(m_ParameterListJson) ? m_ParameterListJson : "{}", parameterListType);
			}
			else
			{
				Debug.LogErrorFormat("Property '{0}' is not invokable becouse parse error on deserialize.", methodId);
			}
		}

		#endregion
	}
}