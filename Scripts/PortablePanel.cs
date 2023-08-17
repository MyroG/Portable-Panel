
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
		Trigger,
		Both
	}

	public enum EClosingBehaviour
	{
		Closing,
		Respawning
	}

	public enum EForceState
	{
		NONE,
		FORCE_CLOSE,
		FORCE_OPEN
	}

	

	[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
	public class PortablePanel : UdonSharpBehaviour
	{
		[Header("See README file for additional help and infos")]
		public GameObject Panel;

		//public bool ClosedByDefault = true;
		public bool TabOnHold = true;
		public EGestureMode GestureMode;
		public EClosingBehaviour CloseBehaviour;
		public bool GrabbablePanel = true;
		public float MaxScale = 9999.0f;
		public float MinScale = 0.1f;
		public float MaxDistanceBeforeClosingThePanel = 2f;
		public float PanelScaleOnDesktop = 0.5f;
		public bool ScaleAndDistanceRelativeToAvatarScale = false;
		private VRCPlayerApi _localPlayer;
		private EGrabbed _grabbed;
		private bool _isRightHandTriggeredGrab , _isLeftHandTriggeredGrab;
		private bool _isRightHandTriggeredTrigger , _isLeftHandTriggeredTrigger;
		private float _timeRightHandGesture , _timeLeftHandGesture;


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

		//Boleans used to open or close the panel without player inputs
		private EForceState _forceStateOfPanel;

		//Her we are saving the transforms of the panel, so we can respawn it with the original scale.
		private Vector3 _position;
		private Quaternion _rotation;
		private Vector3 _scale;

		private bool _isPanelOpen;

		void OnEnable()
		{
			_localPlayer = Networking.LocalPlayer;
			_isRightHandTriggeredGrab = false;
			_isLeftHandTriggeredGrab = false;
			_isRightHandTriggeredTrigger = false;
			_isLeftHandTriggeredTrigger = false;

			_position = Panel.transform.position;
			_rotation = Panel.transform.rotation;
			_scale = Panel.transform.localScale;
		}

		void Start()
		{			
			//if (ClosedByDefault) //not sure if it's really needed
			//{
				ClosePanel();
			//}
			OnStart();
		}

		private void SetPanelScale(float newScale)
		{
			float oldScale = Panel.transform.localScale.x;
			if (oldScale != newScale)
			{
				Panel.transform.localScale = new Vector3(newScale, newScale, newScale);
				OnPanelScaled(oldScale, newScale);
			}
		}

		#region Public methods



		public bool IsPanelOpen()
		{
			return _isPanelOpen;
		}

		

		public void ForceClosePanel()
		{
			if (IsPanelOpen())
			{
				ClosePanel();

				if (!_localPlayer.IsUserInVR())
				{
					//Desktop : this is mostly to make sure that the panel doesn't get reopened if the Desktop player keeps the Tab key pressed
					//VR : I guess no need to do anything?
					_forceStateOfPanel = EForceState.FORCE_CLOSE; 
				}
			}
		}

		public void ForceOpenPanel()
		{
			if (!IsPanelOpen())
			{
				OpenPanel();
				
				PlacePanelInFrontOfPlayer();

				if (!_localPlayer.IsUserInVR())
				{ 
					//doesn't make much sense to set those values for VR players?
					_forceStateOfPanel = EForceState.FORCE_OPEN;
				}
			}
		}

		public bool IsPanelHoldByOneHand()
		{
			return _grabbed == EGrabbed.ONE_HANDED;
		}

		public void TeleportBack()
		{
			Panel.transform.position = _position;
			Panel.transform.rotation = _rotation;
			Panel.transform.localScale = _scale;
		}

		#endregion

		#region Overridable events

		/// <summary>
		/// Gets called when the panel opens
		/// </summary>
		///  <returns>True if the panel needs to be opened.
		/// If you want to open the panel manually, for instance with an animator, then you can `return false;`
		/// </returns>
		public virtual bool OnPanelOpening()
		{
			return true;
		}

		/// <summary>
		/// Gets called when "Start" is called
		/// </summary>
		public virtual void OnStart()
		{
			
		}

		/// <summary>
		/// Gets called when the panel is about to get closed, so it is called when the panel is not closed yet
		/// </summary>
		/// <returns>True if the panel needs to be closed.
		/// If you want to close the panel manually, for instance with an animator, then you can `return false;` instead
		/// </returns>
		public virtual bool OnPanelClosing()
		{
			return true;
		}

		/// <summary>
		/// Gets called when the panel is getting grabbed, either by one hand or with both hands.
		/// If the parameter "GrabbablePanel" is set to false, the the event will ony be called while scaling the panel 8so when it's grabbed with both hands.
		/// </summary>
		public virtual void OnPanelGrab()
		{
		}

		/// <summary>
		/// Gets called when the panel is dropped
		/// </summary>
		public virtual void OnPanelDrop()
		{
		}

		/// <summary>
		/// Gets called when the panel gets scaled
		/// </summary>
		/// <param name="oldScale">The scale of the panel before it got scaled</param>
		/// <param name="oldScale">The new scale of the panel</param>
		/// <example>You could for instance use that method to change what's written on the panel based on the size, or 
		/// change the color based on the speed it gets scaled.</example>
		public virtual void OnPanelScaled(float oldScale, float newScale)
		{
		}

		#endregion

		#region Main code

		private void ClosePanel()
		{
			_grabbed = EGrabbed.NONE;
			_isRightHandTriggeredGrab = false;
			_isLeftHandTriggeredGrab = false;
			_isRightHandTriggeredTrigger = false;
			_isLeftHandTriggeredTrigger = false;

			if (OnPanelClosing())
			{
				if (CloseBehaviour == EClosingBehaviour.Closing)
				{
					Panel.SetActive(false);
				}
				else
				{
					TeleportBack();
				}

				_isPanelOpen = false;
			}
		}

		private void OpenPanel()
		{
			if (OnPanelOpening())
			{
				Panel.SetActive(true);

				_isPanelOpen = true;
			}
		}

		/// <summary>
		/// Scales the value based on the size of the avatar.
		/// If The avatar is 1m80 tall, 1 meter = 1 meter
		/// If the avatar is 1m60 tall, 1 meter will be turned into 1.60*1/1.80 = 0.88 meter
		/// </summary>
		/// <param name="value">The value the needs to be scaled based on the avatar size</param>
		/// <returns></returns>
		protected float ScaleValueToAvatar(float value)
		{
			if (!ScaleAndDistanceRelativeToAvatarScale)
				return value;
#if UNITY_EDITOR
			return value; // A Client Sim bug makes the script crash if "GetAvatarEyeHeightAsMeters" gets called
#else
			return _localPlayer.GetAvatarEyeHeightAsMeters() * value / 1.80f;
#endif
		}

		private bool IsRightHandTriggered()
		{
			if (GestureMode != EGestureMode.Both)
				return _isRightHandTriggeredGrab || _isRightHandTriggeredTrigger;
			return _isRightHandTriggeredGrab && _isRightHandTriggeredTrigger;
		}

		private bool IsLeftHandTriggered()
		{
			if (GestureMode != EGestureMode.Both)
				return _isLeftHandTriggeredGrab || _isLeftHandTriggeredTrigger;
			return _isLeftHandTriggeredGrab && _isLeftHandTriggeredTrigger;
		}

		private void HandleInput(bool value, UdonInputEventArgs args)
		{
			if (IsRightHandTriggered() && IsLeftHandTriggered()
				&& Mathf.Abs(_timeRightHandGesture - _timeLeftHandGesture) < 0.5f)
			{
				//If the grab gesture is used on both hands, we open the panel
				_startDistanceBetweenTwoHands = Vector3.Distance(
						_localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position,
						_localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position
					);

				OnPanelGrab();

				if (!IsPanelOpen() && _forceStateOfPanel != EForceState.FORCE_CLOSE)
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
			else if (!IsRightHandTriggered() && !IsLeftHandTriggered())
			{
				//If the panel is not grabbed anymore, we close the panel under two conditions:
				//- If the panel became twice as small
				//- If the panel is smaller than "MinScale"
				_forceStateOfPanel = EForceState.NONE;

				OnPanelDrop();

				if (IsPanelOpen() && (
					   _startScale / 3.0f > _currentScale)
				)
				{
					ClosePanel();
				}
				_grabbed = EGrabbed.NONE;
			}
			else if (IsRightHandTriggered() || IsLeftHandTriggered())
			{
				//Grab gesture with one hand :
				//- If the player previously grabbed the panel with both hands, we can add a little delay before "ungrabbing" the panel to avoid accidental 
				//  displacements of the panel
				//- If not, then the player didn't used the grab gesture, in that case we will attach the panel on one hand
				if (_grabbed == EGrabbed.BOTH_HANDS && !_eventAlreadySend)
				{
					_eventAlreadySend = true;
					SendCustomEventDelayedSeconds(nameof(EventEnableOneHandMovement), 0.1f);
					return;
				}
				else if (GrabbablePanel && IsPanelOpen())
				{
					AttachToHand();
				}
			}
		}

		public override void InputGrab(bool value, UdonInputEventArgs args)
		{
			if (!_localPlayer.IsUserInVR() || GestureMode == EGestureMode.Trigger)
			{
				return;
			}

			if (args.handType == HandType.RIGHT)
			{
				_isRightHandTriggeredGrab = value;
				_timeRightHandGesture = Time.time;
			}
			else if (args.handType == HandType.LEFT)
			{
				_isLeftHandTriggeredGrab = value;
				_timeLeftHandGesture = Time.time;
			}

			HandleInput(value, args);
		}

		public override void InputUse(bool value, UdonInputEventArgs args)
		{
			if (!_localPlayer.IsUserInVR() || GestureMode == EGestureMode.Grab)
			{
				return;
			}

			if (args.handType == HandType.RIGHT)
			{
				_isRightHandTriggeredTrigger = value;
				_timeRightHandGesture = Time.time;
			}
			else if (args.handType == HandType.LEFT)
			{
				_isLeftHandTriggeredTrigger = value;
				_timeLeftHandGesture = Time.time;
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

		private bool PanelTooFarAway()
		{
			return Vector3.Distance(_localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position, Panel.transform.position)
				 > ScaleValueToAvatar(MaxDistanceBeforeClosingThePanel);
		}

		private void AttachToHand()
		{

			if (!GrabbablePanel)
			{
				_grabbed = EGrabbed.NONE;
				return;
			}
			else
			{
				_grabbed = EGrabbed.ONE_HANDED;

				if (_isLeftHandTriggeredGrab)
					_panelAttachedToHand = VRCPlayerApi.TrackingDataType.LeftHand;
				else
					_panelAttachedToHand = VRCPlayerApi.TrackingDataType.RightHand;

				VRCPlayerApi.TrackingData hand = _localPlayer.GetTrackingData(_panelAttachedToHand);
				_offsetRotation = Quaternion.Inverse(hand.rotation) * Panel.transform.rotation;
				_offsetPosition = Quaternion.Inverse(hand.rotation) * (Panel.transform.position - hand.position);

				OnPanelGrab();
			}
		}

		public void Update()
		{
			//In VR, it is more interesting to use Update, so it is still possible to interact with it while holding the panel and moving around with it.
			if (_localPlayer.IsUserInVR())
			{
				if (_grabbed == EGrabbed.NONE)
				{
					if (IsPanelOpen() && PanelTooFarAway())
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
					Vector3 headPos = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

					_currentScale = Mathf.Clamp(_startScale * Vector3.Distance(left, right) / _startDistanceBetweenTwoHands, ScaleValueToAvatar(MinScale), ScaleValueToAvatar(MaxScale));

					Panel.transform.position = ((left + right) / 2.0f);
					SetPanelScale(_currentScale);
					Panel.transform.LookAt(headPos);
					Panel.transform.forward = -Panel.transform.forward;
				}
			}
		}

		public override void PostLateUpdate()
		{
			//On Desktop, it is more interesting to use PostLateUpdate, so the panel doesn't lag behind.
			if (!_localPlayer.IsUserInVR())
			{
				if (TabOnHold)
				{
					bool tabPressed = Input.GetKey(KeyCode.Tab);

					if ((tabPressed && _forceStateOfPanel != EForceState.FORCE_CLOSE)
						|| _forceStateOfPanel == EForceState.FORCE_OPEN)
					{
						if (!IsPanelOpen())
						{
							OpenPanel();
						}
						PlacePanelInFrontOfPlayer();

						if (tabPressed)
						{
							_forceStateOfPanel = EForceState.NONE;
						}
					}
					else if (!tabPressed || _forceStateOfPanel == EForceState.FORCE_CLOSE)
					{
						if (IsPanelOpen())
						{
							ClosePanel();
						}
						if (!tabPressed)
						{
							_forceStateOfPanel = EForceState.NONE;
						}
					}
				}
				else
				{
					bool tabPressedDown = Input.GetKeyDown(KeyCode.Tab);

					if (!IsPanelOpen() && (tabPressedDown || _forceStateOfPanel == EForceState.FORCE_OPEN))
					{
						OpenPanel();
						PlacePanelInFrontOfPlayer();

						_forceStateOfPanel = EForceState.NONE;
					}
					else if (IsPanelOpen() && (tabPressedDown || _forceStateOfPanel == EForceState.FORCE_CLOSE || PanelTooFarAway()))
					{
						ClosePanel();
						_forceStateOfPanel = EForceState.NONE;
					}
				}
			}
		}

		private void PlacePanelInFrontOfPlayer()
		{
			Quaternion headRot = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
			Vector3 headPos = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

			float distance = ScaleValueToAvatar(0.3f);
			float scale = ScaleValueToAvatar(PanelScaleOnDesktop);
			if (ScaleAndDistanceRelativeToAvatarScale && distance < 0.08f)
			{
				//If the avatar is really small, we need to place the menu a bit further away so it doesn't get clipped by the camera
				distance = 0.08f;
				scale = distance * PanelScaleOnDesktop / 0.3f;
			}
			if (scale < MinScale)
			{
				//Now, if the scale is smaller than MinScale, we need to readjust the scale and the distance.
				distance = MinScale * distance / scale;
				scale = MinScale;
			}

			Panel.transform.position = (headPos + headRot * Vector3.forward * distance);
			SetPanelScale(scale);
			Panel.transform.LookAt(headPos);
			Panel.transform.forward = -Panel.transform.forward;
		}

		#endregion
	}
}
