using System;
using System.Collections.Generic;
using System.Linq;
using Mobcast.Coffee.Toggles;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;


namespace Mobcast.Coffee.Toggles
{
	/// <summary>
	/// Composite toggle.
	/// </summary>
	[ExecuteInEditMode]
	public class CompositeToggle : ParentChildRelatable<CompositeToggle>, ISerializationCallbackReceiver
	{
		static readonly List<Component> s_Components = new List<Component>();


		/// <summary>
		/// 値タイプ.
		/// </summary>
		public enum ValueType
		{
			/// <summary>bool型.</summary>
			Boolean,
			/// <summary>インデックス型.</summary>
			Index,
			/// <summary>カウント型.</summary>
			Count,
			/// <summary>ビットマスク型.</summary>
			Flag,
		}

		/// <summary>トグルの要素数を返します.</summary>
		public virtual int count
		{
			get { return m_Count; }
			set
			{
				m_Count = value;
				Reflesh();
			}
		}

		[SerializeField] int m_Count;

		/// <summary>トグル値変更イベント.</summary>
		[System.Serializable]
		public class OnValueChangeEvent : UnityEvent<CompositeToggle>
		{
		}


		//---- ▼ 公開/シリアライズ設定項目 ▼ ----
		/// <summary>値タイプ.</summary>
		public ValueType valueType { get { return m_ValueType; } }

		[SerializeField]protected ValueType m_ValueType = ValueType.Boolean;

		[SerializeField]protected bool m_ResetValueOnAwake = false;

		/// <summary>トグル値.</summary>
		[SerializeField]protected int m_Value = 1;

		/// <summary>
		/// Gets the synced toggles.
		/// </summary>
		/// <value>The synced toggles.</value>
		public List<CompositeToggle> syncedToggles { get { return m_SyncedToggles; } }
		[SerializeField] List<CompositeToggle> m_SyncedToggles = new List<CompositeToggle>();

		/// <summary>
		/// Gets the group parent.
		/// </summary>
		/// <value>The group parent.</value>
		public CompositeToggle groupParent { get; private set;}

		/// <summary>
		/// Gets the grouped toggles.
		/// </summary>
		/// <value>The grouped toggles.</value>
		public List<CompositeToggle> groupedToggles { get { return m_GroupedToggles; } }

		[SerializeField] List<CompositeToggle> m_GroupedToggles = new List<CompositeToggle>();

		/// <summary>
		/// Gets the toggle properties.
		/// </summary>
		/// <value>The toggle properties.</value>
		public List<Property> toggleProperties { get { return m_ToggleProperties; } }

		[SerializeField]
		List<Property> m_ToggleProperties = new List<Property>();

		/// <summary>
		/// The m activate objects.
		/// </summary>
		[SerializeField]
		List<GameObject> m_ActivateObjects = new List<GameObject>();

		/// <summary>
		/// The m comments.
		/// </summary>
		[SerializeField]
		List<string> m_Comments = new List<string>();

		/// <summary>
		/// The m actions.
		/// </summary>
		[SerializeField]
		List<UnityEvent> m_Actions = new List<UnityEvent>();

		/// <summary>トグルの値が変更された時のコールバック.</summary>
		public OnValueChangeEvent onValueChanged{get {return m_OnValueChanged; }}
		[SerializeField] OnValueChangeEvent m_OnValueChanged;


		//---- ▲ 公開/シリアライズ設定項目 ▲ ----


		/// <summary>通知ロック. 自分の値変更により子トグルが変更された場合、子トグルから発生した通知を無視する.</summary>
		public bool hasLocked { get; protected set; }

		#region ISerializationCallbackReceiver implementation

		public void OnBeforeSerialize()
		{
		}

		public void OnAfterDeserialize()
		{
			Reflesh(true);
		}

		#endregion

		//==== ▼ Unityコールバック ▼ ====

		/// <summary>
		/// Awake this instance.
		/// </summary>
		protected override void Awake()
		{
			base.Awake();

			Reflesh();

			forceNotifyNext = m_ResetValueOnAwake;
			maskValue = maskValue;
		}

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="Mobcast.Coffee.Toggles.CompositeToggle"/> force notify next.
		/// </summary>
		/// <value><c>true</c> if force notify next; otherwise, <c>false</c>.</value>
		public bool forceNotifyNext { get; set; }

		/// <summary>ブール値を取得/設定します.</summary>
		public bool booleanValue { get { return 1 < m_Value; } set { maskValue = value ? 2 : 1; } }

		/// <summary>インデックス値を取得/設定します.</summary>
		public int indexValue { get { return GetCountTailingZero(m_Value); } set { maskValue = 1 << value; } }

		/// <summary>カウント値を取得/設定します.</summary>
		public int countValue { get { return GetCountTailingZero(~m_Value); } set { maskValue = (1 << value) - 1; } }

		/// <summary>取得/設定します.</summary>
		public float valueAsFloat {
			get
			{
				switch (valueType)
				{
					case ValueType.Count: return (float)countValue;
					case ValueType.Flag: return (float)maskValue;
					default: return (float)indexValue;
				}
			}
			set
			{
				switch (valueType)
				{
					case ValueType.Count:
						countValue = (int)value;
						break;
					case ValueType.Flag:
						maskValue = (int)value;
						break;
					default:
						indexValue = (int)value;
						break;
				}
			}
		}

		/// <summary>トグル値を取得/設定します. 与えられた値に応じて、アクションが実行されます.</summary>
		public int maskValue
		{
			get { return m_Value; }
			set
			{
				if (hasLocked)
					return;

				hasLocked = true;

				value = Mathf.Max(value, 0);
				if (forceNotifyNext || m_Value != value)
				{
					forceNotifyNext = false;
					m_Value = value;
					OnObjectUpdate();
				}

				// Notify changes to synced toggles or children.
				//同期設定しているトグルと子トグルに、変更を通知.
				for (int i = 0; i < m_SyncedToggles.Count; i++)
				{
					var toggle = m_SyncedToggles[i];
					if (toggle && toggle != this && !toggle.groupParent)
						toggle.maskValue = m_Value;
				}

				for (int i = 0; i < children.Count; i++)
				{
					var toggle = children[i];
					if (toggle && toggle != this && !toggle.ignoreParent && !toggle.groupParent)
						toggle.maskValue = m_Value;
				}

				hasLocked = false;
			}
		}

		/// <summary>
		/// ブール値を反転、もしくはインデックスを1つ進めます.
		/// ValueTypeがMaskの場合は何も行いません.
		/// </summary>
		public void Toggle()
		{
			if (count <= 0)
				return;

			//booleanまたはindexのみ、クリックで変更可能.
			switch (valueType)
			{
				case ValueType.Boolean:
				case ValueType.Index:
					indexValue = ((indexValue + 1) % count);
					break;
				case ValueType.Count:
					countValue = ((countValue + 1) % (count + 1));
					break;
			}
		}

		/// <summary>
		/// Reflesh the specified inSerialization.
		/// </summary>
		/// <param name="inSerialization">If set to <c>true</c> in serialization.</param>
		public virtual void Reflesh(bool inSerialization = false)
		{
			m_Count = (m_ValueType == ValueType.Boolean) ? 2 : Mathf.Clamp(m_Count, 0, 31);

			FitSize(m_GroupedToggles, 0 < m_GroupedToggles.Count ? count : 0, () => null);

			for (int i = 0; i < m_GroupedToggles.Count; i++)
			{
				var toggle = m_GroupedToggles[i];
				if (!toggle || toggle == this)
					continue;
				
				//子トグルはGroup以外かつ、booleanのみ.
				toggle.m_ValueType = ValueType.Boolean;
				toggle.ignoreParent = true;
				toggle.m_ResetValueOnAwake = false;
				toggle.m_SyncedToggles.Clear();
//				toggle.m_OnValueChanged = new OnValueChangeEvent();
				toggle.SetParent(null);
				toggle.groupParent = this;
				toggle.Reflesh(inSerialization);
			}

			//
			for (int i = 0; i < m_ToggleProperties.Count; i++)
			{
				var target = m_ToggleProperties[i];
				if (!target.hasParseError && target.parameterList != null)
					target.parameterList.FitSize(count);
			}

			FitSize(m_ActivateObjects, 0 < m_ActivateObjects.Count ? count : 0, () => null);
			FitSize(m_Actions, 0 < m_Actions.Count ? count : 0, () => new UnityEvent());
			FitSize(m_Comments, m_Count, () => "");

#if UNITY_EDITOR
			if (!inSerialization)
				UnityEditor.EditorUtility.SetDirty(this);
#endif
		}

		/// <summary>
		/// Fits the size.
		/// </summary>
		/// <param name="self">Self.</param>
		/// <param name="size">Size.</param>
		/// <param name="defaultCreation">Default creation.</param>
		/// <typeparam name="T">The 1st type parameter.</typeparam>
		static void FitSize<T>(List<T> self, int size, System.Func<T> defaultCreation)
		{
			if (size < 0 || self.Count == size)
				return;

			while (size < self.Count)
				self.RemoveAt(self.Count - 1);
			while (self.Count < size)
				self.Add(defaultCreation());
		}

		/// <summary>トグルグループにおいて、自分自身がのインデックス番号を取得します.親トグルがいない場合は-1を返します.</summary>
		public int indexInGroup
		{
			get
			{
				if (!groupParent)
					return -1;

				for (int i = 0; i < groupParent.m_GroupedToggles.Count; i++)
				{
					if (groupParent.m_GroupedToggles[i] == this)
						return i;
				}

				groupParent = null;
				return -1;
			}
		}

		/// <summary>
		/// Raises the object update event.
		/// </summary>
		protected virtual void OnObjectUpdate()
		{
			InvokeToggleTarget(indexValue);
//			components.Clear();


			//isNotifyLocked = true;
			int currentValue = maskValue;


			//トグルグループの場合、子トグルを設定.
			for (int i = 0; i < count; i++)
			{
				bool flag = (0 != (currentValue & (1 << i)));

				if (i < m_GroupedToggles.Count && m_GroupedToggles[i] && m_GroupedToggles[i] != this)
					m_GroupedToggles[i].booleanValue = flag;
				
				if (i < m_ActivateObjects.Count && m_ActivateObjects[i])
					m_ActivateObjects[i].SetActive(flag);
			}

			onValueChanged.Invoke(this);

#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
		}

		/// <summary>
		/// Invokes the toggle target.
		/// </summary>
		/// <param name="index">Index.</param>
		public void InvokeToggleTarget(int index)
		{
			if (index < 0 || count <= index || ValueType.Index < valueType )
				return;

			s_Components.Clear();
			GetComponents(s_Components);
			for (int i = 0; i < m_ToggleProperties.Count; i++)
			{
				try
				{
					m_ToggleProperties[i].Invoke(s_Components.Find(x=>x.GetType() == m_ToggleProperties[i].methodTargetType), index);
				}
				catch (Exception ex)
				{
					Debug.LogException(ex);
				}
			}
			s_Components.Clear();

			if (index < m_Actions.Count)
			{
				try
				{
					m_Actions[index].Invoke();
				}
				catch (Exception ex)
				{
					Debug.LogException(ex);
				}
			}
		}

		/// <summary>最後尾から連続するゼロビットの数を返す.</summary>
		static int GetCountTailingZero(long x)
		{
			if (x == 0)
				return -1;

			ulong y = (ulong)(x & -x);
			int index = (int)((y * 0x03F566ED27179461UL) >> 58);
			if (countingTailingZeroTable == null)
			{
				countingTailingZeroTable = new int[64];
				ulong hash = 0x03F566ED27179461UL;
				for (int i = 0; i < 64; i++)
				{
					countingTailingZeroTable[hash >> 58] = i;
					hash <<= 1;
				}
			}
			return countingTailingZeroTable[index];
		}

		static int[] countingTailingZeroTable;

	}
}