
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace myro
{
	public enum EGrabbed
	{
		NONE,
		BOTH_HANDS,
		ONE_HANDED
	}

	public enum EGestureMode
	{
		Grab,
		Trigger
	}

	[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
	public class PortablePanel : UdonSharpBehaviour
	{
		[Header("See README file for additional help and infos")]
		public GameObject Panel;
		public EGestureMode GestureMode;
		public bool GrabbablePanel = true;
		public float MaxScale = 9999.0f;
		public float MinScale = 0.1f;
		public float MaxDistanceBeforeClosingThePanel = 2f;
		public float PanelScaleOnDesktop = 0.5f;

		private VRCPlayerApi _localPlayer;
		private EGrabbed _grabbed;
		bool _isRightHandTriggered , _isLeftHandTriggered;


		//The variables bellow are used to calculate the position and the scale of the panel once it's hold with both hand
		private float _startDistanceBetweenTwoHands;
		private float _startScale;
		private float _currentScale;

		//The variables bellow are used to calculate the position of the panel once it's hold with one hand
		private Quaternion _offsetRotation;
		private Vector3 _offsetPosition;
		private VRCPlayerApi.TrackingDataType _panelAttachedToHand;

		//This bool is only used to avoid event spams
		private bool _eventAlreadySend = false;

		void OnEnable()
		{
			_localPlayer = Networking.LocalPlayer;
			_grabbed = EGrabbed.NONE;
			_isRightHandTriggered = false;
			_isLeftHandTriggered = false;
			ClosePanel();
		}

		private void SetPanelScale(float scale)
		{
			Panel.transform.localScale = new Vector3(scale, scale, scale);
		}

		#region Public methods



		public bool IsPanelOpen()
		{
			return Panel.activeSelf;
		}

		public bool IsPanelHoldByOneHand()
		{
			return _grabbed == EGrabbed.ONE_HANDED;
		}

		#endregion

		#region Overridable events

		public virtual void OnPanelOpen()
		{

		}

		public virtual void OnPanelClose()
		{

		}

		#endregion

		#region Main code

		private void OpenPanel()
		{
			Panel.SetActive(true);
			OnPanelOpen();
		}

		private void ClosePanel()
		{
			Panel.SetActive(false);
			OnPanelClose();
		}

		private void HandleInput(bool value, UdonInputEventArgs args)
		{
			if (args.handType == HandType.RIGHT)
				_isRightHandTriggered = value;
			else if (args.handType == HandType.LEFT)
				_isLeftHandTriggered = value;

			if (_isRightHandTriggered && _isLeftHandTriggered)
			{
				//If the grab gesture is used on both hands, we open the panel
				_startDistanceBetweenTwoHands = Vector3.Distance(
						_localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position,
						_localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position
					);

				if (!IsPanelOpen())
				{
					OpenPanel();
					_startScale = _startDistanceBetweenTwoHands;
				}
				else
				{
					_startScale = Panel.transform.localScale.x;
				}
				_grabbed = EGrabbed.BOTH_HANDS;
			}
			else if (!_isRightHandTriggered && !_isLeftHandTriggered)
			{
				//If the panel is not grabbed anymore, we close the panel under two conditions:
				//- If the panel became twice as small
				//- If the panel is smaller than "MinScale"

				if (IsPanelOpen() && (
					   _startScale / 2.0f > _currentScale
					|| _currentScale < MinScale)
				)
				{
					ClosePanel();
				}
				_grabbed = EGrabbed.NONE;
			}
			else
			{
				//Grab gesture with one hand :
				//- If the player previously grabbed the panel with both hands, we can add a little delay before "ungrabbing" the panel to avoid accidental 
				//  displacements of the panel
				//- If not, then the player didn't used the grab gesture, in that case we will attach the panel on one hand
				if (_grabbed == EGrabbed.BOTH_HANDS && !_eventAlreadySend)
				{
					_eventAlreadySend = true;
					SendCustomEventDelayedSeconds(nameof(EventEnableOneHandMovement), 0.2f);
					return;
				}
				else if (GrabbablePanel)
				{
					AttachToHand();
				}
			}
		}

		public override void InputGrab(bool value, UdonInputEventArgs args)
		{
			if (!_localPlayer.IsUserInVR() || GestureMode != EGestureMode.Grab)
			{
				return;
			}

			HandleInput(value, args);
		}

		public override void InputUse(bool value, UdonInputEventArgs args)
		{
			if (!_localPlayer.IsUserInVR() || GestureMode != EGestureMode.Trigger)
			{
				return;
			}

			HandleInput(value, args);
		}

		public void EventEnableOneHandMovement()
		{
			if (_grabbed == EGrabbed.BOTH_HANDS)
			{
				AttachToHand();
			}
			_eventAlreadySend = false;
		}

		private void AttachToHand()
		{
			_grabbed = EGrabbed.ONE_HANDED;
			if (_isLeftHandTriggered)
				_panelAttachedToHand = VRCPlayerApi.TrackingDataType.LeftHand;
			else
				_panelAttachedToHand = VRCPlayerApi.TrackingDataType.RightHand;

			VRCPlayerApi.TrackingData hand = _localPlayer.GetTrackingData(_panelAttachedToHand);
			_offsetRotation = Quaternion.Inverse(hand.rotation) * Panel.transform.rotation;
			_offsetPosition = Quaternion.Inverse(hand.rotation) * (Panel.transform.position - hand.position);
		}

		public void Update()
		{
			Vector3 headPos = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

			if (_localPlayer.IsUserInVR())
			{
				//VR
				if (_grabbed == EGrabbed.NONE)
				{
					if (Vector3.Distance(headPos, Panel.transform.position) > MaxDistanceBeforeClosingThePanel)
					{
						ClosePanel();
					}
					return;
				}
				else if (_grabbed == EGrabbed.ONE_HANDED)
				{
					VRCPlayerApi.TrackingData hand = _localPlayer.GetTrackingData(_panelAttachedToHand);

					Panel.transform.position = (hand.position + (hand.rotation * _offsetPosition));
					Panel.transform.rotation = (hand.rotation * _offsetRotation);
				}
				else
				{
					Vector3 left = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
					Vector3 right = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;

					_currentScale = Mathf.Min(MaxScale,_startScale * Vector3.Distance(left, right) / _startDistanceBetweenTwoHands);

					Panel.transform.position = ((left + right) / 2.0f);
					SetPanelScale(_currentScale);
					Panel.transform.LookAt(headPos);
					Panel.transform.forward = -Panel.transform.forward;
				}
			}
			else if (Input.GetKey(KeyCode.Tab))
			{
				if (!IsPanelOpen())
				{
					Quaternion headRot = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;

					if (_isLeftHandTriggered)
						_panelAttachedToHand = VRCPlayerApi.TrackingDataType.LeftHand;
					else
						_panelAttachedToHand = VRCPlayerApi.TrackingDataType.RightHand;

					OpenPanel();

					Panel.transform.position = (headPos + headRot * Vector3.forward * .3f);
					SetPanelScale(PanelScaleOnDesktop);
					Panel.transform.LookAt(headPos);
					Panel.transform.forward = -Panel.transform.forward;
				}
			}
			else if (IsPanelOpen())
			{
				ClosePanel();
			}
		}

		#endregion
	}
}
