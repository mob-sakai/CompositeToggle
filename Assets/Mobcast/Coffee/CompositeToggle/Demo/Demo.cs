using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Mobcast.Coffee.Toggles
{
	public class Demo : MonoBehaviour
	{
		[SerializeField] 
		Text text;
		
		// Update is called once per frame
		public void SetValue(float value)
		{
			if (text)
				text.text = value.ToString();
		}

		public void CreateInstance(GameObject obj)
		{
			StartCoroutine(CreateInstanceOnNextFrame(obj));
		}

		IEnumerator CreateInstanceOnNextFrame(GameObject obj)
		{
			yield return new WaitForEndOfFrame();

			var go = GameObject.Instantiate(obj);
			go.transform.SetParent(transform.parent);
			go.transform.localPosition = Vector3.zero;
			go.transform.localScale = Vector3.one;
			go.transform.localRotation = Quaternion.identity;

			Button b = go.GetComponentInChildren<Button>();
			CompositeToggle c = go.GetComponentInChildren<CompositeToggle>();
			b.onClick.AddListener(c.Toggle);
		}
	}
}