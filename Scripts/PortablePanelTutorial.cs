
using myro;
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class PortablePanelTutorial : UdonSharpBehaviour
{
	public PortablePanel PortablePanel;
	public float PlaceInFrontOfPlayerForXSeconds = 10.0f;

	[TextArea(3, 12)]
	public string TextVR = "{0} : Press while bringing your hands together, then pull apart to open the panel.\n[Grab+Trigger] in one-handed mode.";

	[TextArea(3, 12)]
	public string TextDesktop = "Press [Tab] to open the panel";

	[Header("Used internally, do not change, except if really needed")]
	public Image LeftController;
	public Image RightController;
	
	public Sprite ControllerTrigger;
	public Sprite ControllerGrab;
	public Sprite ControllerBoth;

	public TextMeshProUGUI TutorialTextComponentVR;
	public TextMeshProUGUI TutorialTextComponentDesktop;

	public GameObject VR;
	public GameObject Desktop;

	private VRCPlayerApi _localPlayer;

	private const float TUTORIAL_DISTANCE_FROM_FACE = 1.0f;
	void Start()
    {
		_localPlayer = Networking.LocalPlayer;

#if UNITY_ANDROID
		if (!_localPlayer.IsUserInVR())
		{
			gameObject.SetActive(false);
			return;
		}
#endif

		if (PortablePanel == null)
		{
			Debug.LogError("On the TutorialForUser prefab, the Portable Panel reference is null, you need to set it!");
			return;
		}

		PortablePanel.RegisterTutorial(this);

		switch (PortablePanel.GestureMode)
		{
			case EGestureMode.Grab:
				SetSprite(ControllerGrab);
				break;
			case EGestureMode.Trigger:
				SetSprite(ControllerTrigger);
				break;
			case EGestureMode.Both:
				SetSprite(ControllerBoth);
				break;
		}

		TutorialTextComponentVR.text = System.String.Format(TextVR, GetGesture());
		TutorialTextComponentDesktop.text = System.String.Format(TextDesktop, GetGesture());

			VR.SetActive(_localPlayer.IsUserInVR());
		Desktop.SetActive(!_localPlayer.IsUserInVR());
	}

	private void SetSprite(Sprite img)
	{
		LeftController.sprite = img;
		RightController.sprite = img;
	}

	private string GetGesture()
	{
		switch (PortablePanel.GestureMode)
		{
			case EGestureMode.Grab:
				return "[Grab]";
			case EGestureMode.Trigger:
				return "[Trigger]";
			case EGestureMode.Both:
				return "[Grab+Trigger]";
		}
		return "[unknown]";
	}

	public override void OnPlayerJoined(VRCPlayerApi player)
	{
		if (!player.isLocal || PlaceInFrontOfPlayerForXSeconds <= 0)
		{
			return;
		}

		SendCustomEventDelayedSeconds(nameof(_StopTracking), PlaceInFrontOfPlayerForXSeconds);
	}

	public void _StopTracking()
	{
		gameObject.SetActive(false);
	}

	public override void PostLateUpdate()
	{
		if (PlaceInFrontOfPlayerForXSeconds <= 0)
		{
			return;
		}

		VRCPlayerApi.TrackingData head = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
		transform.position = (head.position + head.rotation * Vector3.forward * TUTORIAL_DISTANCE_FROM_FACE);
		transform.LookAt(head.position);
		transform.forward = -transform.forward;
	}

	internal void _PanelGotOpened()
	{
		if (PlaceInFrontOfPlayerForXSeconds > 0)
		{
			//When we open the panel, we only want to close the tutorial if it isn't placed in world space (so, only in view space)
 			_StopTracking();
		}
	}
}
