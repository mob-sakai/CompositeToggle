using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Mobcast.Coffee.Toggles
{
	/// <summary>
	/// I parameter list.
	/// </summary>
	public interface IParameterList
	{
		int count { get; }

		object GetObject(int index);

		void SetObject(int index, object obj);

		void FitSize(int size);

		string ToJson();
	}

	/// <summary>
	/// Parameter list.
	/// </summary>
	[System.Serializable]
	public abstract class ParameterList<T> : IParameterList
	{
		protected virtual T defaultObject { get { return default(T); } }

		[SerializeField]
		T[] m_List = new T[]{};

		object[] m_BoxedList;

		/// <summary>
		/// Gets the count.
		/// </summary>
		/// <value>The count.</value>
		public int count { get { return m_List.Length; } }

		/// <summary>
		/// Gets the raw.
		/// </summary>
		/// <returns>The raw.</returns>
		/// <param name="index">Index.</param>
		public T GetRaw(int index)
		{
			return m_List[index];
		}

		/// <summary>
		/// Gets the object.
		/// </summary>
		/// <returns>The object.</returns>
		/// <param name="index">Index.</param>
		public object GetObject(int index)
		{
			if (m_BoxedList == null)
				m_BoxedList = new object[count];
			else if (m_BoxedList.Length != count)
				Array.Resize(ref m_BoxedList, count);
//			
//			while (m_BoxedList.Count < index + 1)
//				m_BoxedList.Add(null);

			if (object.ReferenceEquals(m_BoxedList[index], null))
				m_BoxedList[index] = m_List[index];

			return m_BoxedList[index];
		}

		/// <summary>
		/// Sets the object.
		/// </summary>
		/// <param name="index">Index.</param>
		/// <param name="obj">Object.</param>
		public void SetObject(int index, object obj)
		{
			if (m_BoxedList == null)
				m_BoxedList = new object[count];
			else if (m_BoxedList.Length != count)
				Array.Resize(ref m_BoxedList, count);
//			if (m_BoxedList == null)
//				m_BoxedList = new List<object>();
//			
//			while (m_BoxedList.Count < index + 1)
//				m_BoxedList.Add(null);

			m_BoxedList[index] = obj;
			m_List[index] = (T)obj;
		}

		/// <summary>
		/// Fits the size.
		/// </summary>
		/// <param name="size">Size.</param>
		public void FitSize(int size)
		{
			if (count == size)
				return;

			Array.Resize(ref m_List, size);
//
//			while (size < m_List.Count)
//				m_List.RemoveAt(m_List.Count - 1);
//			while (m_List.Count < size)
//				m_List.Add(defaultObject);
		}

		/// <summary>
		/// Tos the json.
		/// </summary>
		/// <returns>The json.</returns>
		public string ToJson()
		{
			return JsonUtility.ToJson(this);
		}
	}

	//
	[System.Serializable]
	public sealed class EventParameterList : ParameterList<UnityEvent>
	{
		protected override UnityEvent defaultObject { get { return new UnityEvent(); } }
	}

	[System.Serializable]
	public sealed class ColorParameterList : ParameterList<Color>
	{
		protected override Color defaultObject { get { return Color.white; } }
	}

	[System.Serializable]
	public sealed class ColorBlockParameterList : ParameterList<ColorBlock>{}

	[System.Serializable]
	public sealed class BoolParameterList : ParameterList<bool>{}

	[System.Serializable]
	public sealed class IntParameterList : ParameterList<int>{}

	[System.Serializable]
	public sealed class LongParameterList : ParameterList<long>{}

	[System.Serializable]
	public sealed class FloatParameterList : ParameterList<float>{}

	[System.Serializable]
	public sealed class StringParameterList : ParameterList<string>{}

	[System.Serializable]
	public sealed class Vector2ParameterList : ParameterList<Vector2>{}

	[System.Serializable]
	public sealed class Vector3ParameterList : ParameterList<Vector3>{}

	[System.Serializable]
	public sealed class Vector4ParameterList : ParameterList<Vector4>{}

	[System.Serializable]
	public sealed class GradientParameterList : ParameterList<Gradient>
	{
		protected override Gradient defaultObject { get { return new Gradient(); } }
	}

	[System.Serializable]
	public sealed class AnimationCurveParameterList : ParameterList<AnimationCurve>
	{
		protected override AnimationCurve defaultObject { get { return AnimationCurve.Linear(0, 0, 1, 1); } }
	}

	[System.Serializable]
	public sealed class LayerParameterList : ParameterList<LayerMask>{}

	[System.Serializable]
	public sealed class EnumParameterList : ParameterList<Enum>{}

	[System.Serializable]
	public sealed class ObjectParameterList : ParameterList<Object>{}
}