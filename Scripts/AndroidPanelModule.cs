
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;



namespace myro
{
	[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
	public class AndroidPanelModule : UdonSharpBehaviour
	{
		[Header("Set your panel here")]
		public PortablePanel PortablePanelInstance;

		[Header("Used internally, do not change, except if really needed")]
		public Canvas CanvasInstance;

		void Start()
		{
			CanvasInstance.gameObject.SetActive(false);
		}

		public void ToggleMenu()
		{
			if (PortablePanelInstance.IsPanelOpen())
			{
				PortablePanelInstance.ForceClosePanel();
			}
			else
			{
				PortablePanelInstance.ForceOpenPanel();
			}
		}

		public bool IsPlayerOnAndroid()
		{
#if UNITY_ANDROID
		return !Networking.LocalPlayer.IsUserInVR();
#else
			return false;
#endif
		}

		public override void OnPlayerJoined(VRCPlayerApi player)
		{
			if (player.isLocal)
			{
				CanvasInstance.gameObject.SetActive(IsPlayerOnAndroid());
			}
		}
	}
}
