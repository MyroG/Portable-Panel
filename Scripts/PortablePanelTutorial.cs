
using myro;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class PortablePanelTutorial : UdonSharpBehaviour
{
	public PortablePanel PortablePanel;
	public float PlaceInFrontOfPlayerForXSeconds = 7.0f;

	[TextArea(3, 12)]
	public string TextVR = "To open the panel, bring your hands together, press {0}, and pull apart.\nIn one-handed mode, do the same with one hand.";

	[TextArea(3, 12)]
	public string TextDesktop = "To open the panel, press [Tab]";

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
}
