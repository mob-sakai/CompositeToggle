using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Mobcast.Coffee.Toggles
{
	/// <summary>
	/// Parent child relatable.
	/// </summary>
	[ExecuteInEditMode]
	public class ParentChildRelatable<T> : MonoBehaviour where T : ParentChildRelatable<T>
	{
		static List<T> s_TempRelatables = new List<T>();

		/// <summary>親トグル.</summary>
		public T parent { get { return m_Parent; } }

		T m_Parent;

		/// <summary>子トグルリスト.</summary> 
		public List<T> children { get { return m_Children; } }

		List<T> m_Children = new List<T>();

		public Transform cachedTransform { get { if (m_CachedTransform == null) m_CachedTransform = transform; return m_CachedTransform; } }

		protected Transform m_CachedTransform;

		/// <summary>親CompositeToggleの値が変更されても自分の値を変更しません.</summary>
		public bool ignoreParent
		{
			get { return m_IgnoreParent; }
			set
			{
				if (m_IgnoreParent == value)
					return;
				
				m_IgnoreParent = value;
				OnTransformParentChanged();
			}
		}

		[SerializeField] bool m_IgnoreParent = false;

		//==== ▼ Unityコールバック ▼ ====
		protected virtual void Awake()
		{
			GetComponentsInChildren<T>(true, s_TempRelatables);
			for (int i = 0; i < s_TempRelatables.Count; i++)
			{
				T relatable = s_TempRelatables[i];
				if (relatable == this || !relatable.ignoreParent)
				{
					relatable.OnTransformParentChanged();
				}
			}
			s_TempRelatables.Clear();
		}

		/// <summary>
		/// コンポーネントの破棄コールバック.
		/// インスタンスが破棄された時にコールされます.
		/// </summary>
		protected virtual void OnDestroy()
		{
			SetParent(null);
		}

		/// <summary>
		/// 親Transformが変更されたときのコールバック.
		/// groupByTransform=trueの時、Transformヒエラルキーから自動的に親子関係を結びます.
		/// </summary>
		public virtual void OnTransformParentChanged()
		{
			T newParent = null;
			if (!ignoreParent)
			{
				//親階層から、一番近いCompositeToggleを検索.
				var parentTransform = transform.parent;
				while (parentTransform && newParent == null)
				{
					newParent = parentTransform.GetComponent<T>();
					parentTransform = parentTransform.parent;
				}
			}
			SetParent(newParent);
		}

		/// <summary>
		/// 複合コンポーネントの親子関係を設定します.
		/// Transformヒエラルキーに関係なく、コンポーネントの親子関係を結ぶことができます.
		/// </summary>
		/// <param name="newParent">親となる複合コンポーネント.</param>
		protected void SetParent(T newParent)
		{
			if (m_Parent != newParent && this != newParent)
			{
				if (m_Parent && m_Parent.m_Children.Contains(this as T))
				{
					m_Parent.m_Children.Remove(this as T);
					m_Parent.m_Children.RemoveAll(x => x == null);
				}
				m_Parent = newParent;
			}

			if (m_Parent && !m_Parent.m_Children.Contains(this as T))
			{
				m_Parent.m_Children.Add(this as T);
			}
		}
	}
}