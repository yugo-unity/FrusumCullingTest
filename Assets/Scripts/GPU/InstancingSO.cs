using UnityEngine;


namespace InstancingFeature
{
	[CreateAssetMenu(fileName = "InstancingSO", menuName = "Scriptable Objects/InstancingSO")]
	public class InstancingSO : ScriptableObject
	{
		[SerializeField]
		public InstancingStatic[] vatInstances;
	}
}
