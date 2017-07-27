using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Mobcast.Coffee.Toggles
{
	/// <summary>
	/// Style asset.
	/// </summary>
	[CreateAssetMenu]
	public class StyleAsset : ScriptableObject
	{
		static readonly HashSet<int> s_LockedProperties = new HashSet<int>();
		static readonly HashSet<int> s_LockedStyleAssets = new HashSet<int>();

		/// <summary>
		/// Gets the base style asset.
		/// </summary>
		/// <value>The base style asset.</value>
		public StyleAsset baseStyleAsset { get { return m_BaseStyleAsset; } }

		[SerializeField] StyleAsset m_BaseStyleAsset;

		/// <summary>
		/// Gets the properties.
		/// </summary>
		/// <value>The properties.</value>
		public List<Property> properties { get { return m_Properties; } }

		[SerializeField] List<Property> m_Properties = new List<Property>();

		/// <summary>
		/// Gets the property enumerator.
		/// </summary>
		/// <returns>The property enumerator.</returns>
		public IEnumerable<Property> GetPropertyEnumerator()
		{
			s_LockedProperties.Clear();
			foreach (var styleAsset in GetStyleAssetEnumerator())
			{
				foreach (var property in styleAsset.m_Properties)
					if (property != null && !property.hasParseError && s_LockedProperties.Add(property.methodId.GetHashCode()))
						yield return property;
			}
			s_LockedProperties.Clear();
			yield break;
		}

		/// <summary>
		/// Gets the style asset enumerator.
		/// </summary>
		/// <returns>The style asset enumerator.</returns>
		public IEnumerable<StyleAsset> GetStyleAssetEnumerator()
		{
			s_LockedStyleAssets.Clear();
			var styleAsset = this;
			while (styleAsset && s_LockedStyleAssets.Add(styleAsset.GetInstanceID()))
			{
				yield return styleAsset;
				styleAsset = styleAsset.baseStyleAsset;
			}
			s_LockedStyleAssets.Clear();
			yield break;
		}

		/// <summary>
		/// Contains the specified styleAsset.
		/// </summary>
		/// <param name="styleAsset">Style asset.</param>
		public bool Contains(StyleAsset styleAsset)
		{
			return styleAsset && GetStyleAssetEnumerator().Any(x => object.ReferenceEquals(x, styleAsset));
		}
	}
}