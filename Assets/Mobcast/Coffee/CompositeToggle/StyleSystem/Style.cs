using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mobcast.Coffee.Toggles
{
	/// <summary>
	/// Style Component.
	/// </summary>
	[ExecuteInEditMode]
	public class Style : MonoBehaviour
	{
		static readonly List<Component> s_Components = new List<Component>();
		static readonly Dictionary<Type, UnityEngine.Object> s_ObjectMap = new Dictionary<Type, UnityEngine.Object>();


		/// <summary>
		/// Gets or sets the style.
		/// </summary>
		/// <value>The style.</value>
		public StyleAsset styleAsset
		{
			get { return m_StyleAsset; }
			set
			{
				if (m_StyleAsset == value)
					return;
				
				m_StyleAsset = value;
				if (isActiveAndEnabled)
					LoadStyle();
			}
		}

		[SerializeField] StyleAsset m_StyleAsset;

		/// <summary>
		/// The m ignore properties.
		/// </summary>
		[SerializeField] string[] m_IgnoreProperties;

		// Use this for initialization
		void OnEnable()
		{
			LoadStyle();
		}

		/// <summary>
		/// Applies the style.
		/// </summary>
		public void LoadStyle()
		{
			if (!this || isActiveAndEnabled && !styleAsset)
				return;

			// Get components in self GameObject.
			s_Components.Clear();
			s_ObjectMap.Clear();
			GetComponents<Component>(s_Components);

			//
			foreach (var c in s_Components)
			{
				if (!s_ObjectMap.ContainsKey(c.GetType()))
					s_ObjectMap.Add(c.GetType(), c);
			}

			//
			foreach (var property in styleAsset.GetPropertyEnumerator())
			{
				if(Array.IndexOf(m_IgnoreProperties, property.methodId) <0)
					property.Invoke(s_Components.Find(x=>x.GetType() == property.methodTargetType), 0);
			}

			s_Components.Clear();
			s_ObjectMap.Clear();
#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(this);
#endif
		}
	}
}