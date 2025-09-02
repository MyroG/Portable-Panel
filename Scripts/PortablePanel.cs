using PortMidi;
using System;
using System.Linq;
using UdonSharp;
using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.Windows;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using static VRC.Dynamics.CollisionShapes;

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

	public enum EAllowedInputs
	{
		GRAB_LEFT,
		GRAB_RIGHT,
		TRIGGER_LEFT,
		TRIGGER_RIGHT
	}

	[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
	[DefaultExecutionOrder(500)]
	public class PortablePanel : UdonSharpBehaviour
	{
		[Header("See README file for additional help and infos")]
		public GameObject Panel;
		private Transform _panelTransf;

		//public bool ClosedByDefault = true;
		public bool TabOnHold = true;
		public EGestureMode GestureMode;
		public EClosingBehaviour CloseBehaviour;
		private PortablePanelPickupModule _pickupModule;
		[SerializeField] private bool _isPickupable = true;

		public float MaxScale = 9999.0f;
		public float MinScale = 0.1f;
		public float MaxDistanceBeforeClosingThePanel = 2f;
		public float PanelScaleOnDesktop = 0.5f;

		public bool SetOwnerOnPickup = false;

		[Header("Advanced settings, change them only if you really need to")]
		[SerializeField]
		private bool _delayInitialisation = false;

		private VRCPlayerApi _localPlayer;
		private EGrabbed _grabbed;
		private DataList _triggeredInputList;
		private float _timeStartTrigger, _timeEndTrigger;

		//The variables bellow are used to calculate the position and the scale of the panel once it's hold with both hand
		private float _startDistanceBetweenTwoHands;
		private Vector3 _oneHandedOrigin;
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
		private bool _init;
		private bool _isUsingViveControllers;

		private const float TIME_INTERVAL_HAND_GESTURE = 0.3f;
		private const float MAX_DISTANCE_HAND_GESTURE = 0.25f;
		private const float PLACEMENT_DISTANCE_FROM_HEAD = 0.3f;
		private const float CLOSING_HAND_DISTANCE = 0.15f;

		void OnEnable()
		{
			_localPlayer = Networking.LocalPlayer;
			_triggeredInputList = new DataList();
			_panelTransf = Panel.transform;	
		}

		private void Start()
		{
			_isUsingViveControllers = IsViveController();

			SetRespawnPoint(_panelTransf.position,
				_panelTransf.rotation,
				_panelTransf.localScale);

			if (!_delayInitialisation)
				Initialisation();
		}

		public override void OnPlayerJoined(VRCPlayerApi player)
		{
			if (_delayInitialisation && player.isLocal)
				Initialisation();
		}

		public void Initialisation()
		{
			CloseOrRespawnPanel();
			OnStart();
			_init = true;
		}

		private void SetPanelScale(float newScale)
		{
			float oldScale = _panelTransf.localScale.x;
			if (oldScale != newScale)
			{
				_panelTransf.localScale = new Vector3(newScale, newScale, newScale);
				OnPanelScaled(oldScale, newScale);
			}
		}

		public void SetPickupModule(PortablePanelPickupModule pickupModule)
		{
			_pickupModule = pickupModule;
		}

		#region Public methods

		/// <summary>
		/// Sets the "pickupable" state of your panel. This will also work for VRCPickups
		/// </summary>
		/// <param name="newPickupableState">The new pickupable state</param>
		public void SetPickupable(bool newPickupableState)
		{
			_isPickupable = newPickupableState;
			if (_pickupModule != null)
			{
				if (newPickupableState)
				{
					_pickupModule.EnablePickup();
				}
				else
				{
					_pickupModule.DisablePickup();
				}
			}
		}

		/// <summary>
		/// Toggles the "pickupable" state of your panel. This will also work for VRCPickups
		/// </summary>
		public void _TogglePickupable()
		{
			SetPickupable(!_isPickupable);
		}

		/// <summary>
		/// Return true if the panel is pickupable
		/// </summary>
		/// <returns></returns>
		public bool IsPickupable()
		{
			return _isPickupable;
		}


		public bool IsPanelOpen()
		{
			return _isPanelOpen;
		}

		

		public void ForceClosePanel()
		{
			if (IsPanelOpen())
			{
				CloseOrRespawnPanel();

				if (!_localPlayer.IsUserInVR())
				{
					//Desktop : this is mostly to make sure that the panel doesn't get reopened if the Desktop player keeps the Tab key pressed
					//VR : I guess no need to do anything?
					_forceStateOfPanel = EForceState.FORCE_CLOSE; 
				}
			}
		}

		public void ForceOpenPanel(float unscaledDistance)
		{
			if (!IsPanelOpen())
			{
				OpenPanel();
				
				PlacePanelInFrontOfPlayer(unscaledDistance);

				if (!_localPlayer.IsUserInVR())
				{ 
					_forceStateOfPanel = EForceState.FORCE_OPEN;
				}
			}
		}

		public void ForceOpenPanel()
		{
			ForceOpenPanel(PLACEMENT_DISTANCE_FROM_HEAD);
		}

		public bool IsPanelHoldByOneHand()
		{
			return _grabbed == EGrabbed.ONE_HANDED;
		}

		public void SetRespawnPoint(Vector3 position, Quaternion rotation, Vector3 scale)
		{
			_position = position;
			_rotation = rotation;
			_scale = scale;
		}

		private void SetOwner()
		{
			if (!SetOwnerOnPickup) return;

			if (!Networking.IsOwner(Panel))
				Networking.SetOwner(_localPlayer, Panel);
		}

		public void RespawnPanel()
		{
			//we currently just need to close it
			CloseOrRespawnPanel();
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

		private void CloseOrRespawnPanel()
		{
			_grabbed = EGrabbed.NONE;
			_triggeredInputList = new DataList();

			if (OnPanelClosing())
			{
				if (CloseBehaviour == EClosingBehaviour.Closing)
				{
					Panel.SetActive(false);
				}
				else
				{
					RespawnToOriginalLocation();
				}
			}
			_isPanelOpen = false;
		}

		private void OpenPanel()
		{
			if (OnPanelOpening())
			{
				Panel.SetActive(true);
			}
			_isPanelOpen = true;
		}

		public void RespawnToOriginalLocation()
		{
			SetOwner();
			_panelTransf.position = _position;
			_panelTransf.rotation = _rotation;
			_panelTransf.localScale = _scale;
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
#if UNITY_EDITOR
			return value; // An older Client Sim bug makes the script crash if "GetAvatarEyeHeightAsMeters" gets called.
			// It should be fixed on newer version of ClientSim, but I'll keep it to ensure it still works on older SDKs
#else
			return _localPlayer.GetAvatarEyeHeightAsMeters() * value / 1.80f;
#endif
		}

		private void UpdateTriggeredInputList(EAllowedInputs input, bool isTriggered)
		{
			if (!isTriggered)
			{
				_triggeredInputList.Remove(System.Convert.ToInt32(input));
			}
            else if (IsValidInputToOpenPanel(input))
            {
				_triggeredInputList.Add(System.Convert.ToInt32(input));
				if (_triggeredInputList.Count == 1)
				{
					_timeStartTrigger = Time.time;
				}
				_timeEndTrigger = Time.time;
			}
		}

		private bool IsLeftControllerOn()
		{
			string[] inputs = UnityEngine.Input.GetJoystickNames();
			foreach (string input in inputs)
			{
				if (input.ToLower().Contains("left"))
					return true;
			}
			return false;
		}

		private bool IsRightControllerOn()
		{
			string[] inputs = UnityEngine.Input.GetJoystickNames();
			foreach (string input in inputs)
			{
				if (input.ToLower().Contains("right"))
					return true;
			}
			return false;
		}

		private bool IsOneHanded()
		{
			return !IsRightControllerOn() || !IsLeftControllerOn();
		}

		private DataList GetValidInputs()
		{
			DataList ret = new DataList();

			if (IsLeftControllerOn())
			{
				if (GestureMode == EGestureMode.Both || GestureMode == EGestureMode.Grab)
					ret.Add(Convert.ToInt32(EAllowedInputs.GRAB_LEFT));

				if (GestureMode == EGestureMode.Both || GestureMode == EGestureMode.Trigger)
					ret.Add(Convert.ToInt32(EAllowedInputs.TRIGGER_LEFT));
			}

			if (IsRightControllerOn())
			{
				if (GestureMode == EGestureMode.Both || GestureMode == EGestureMode.Grab)
					ret.Add(Convert.ToInt32(EAllowedInputs.GRAB_RIGHT));

				if (GestureMode == EGestureMode.Both || GestureMode == EGestureMode.Trigger)
					ret.Add(Convert.ToInt32(EAllowedInputs.TRIGGER_RIGHT));
			}

			return ret;
		}
		private bool IsValidInputToOpenPanel(EAllowedInputs inputToCheck)
		{
			DataList validInputs = GetValidInputs();
			return validInputs.Contains(Convert.ToInt32(inputToCheck));
		}

		private bool IsDoingOpeningPanelGesture()
		{
			DataList validInputs = GetValidInputs();

			for (int i = 0; i < validInputs.Count; i++)
			{
				if (!_triggeredInputList.Contains(validInputs[i]))
				{
					return false;
				}
			}
			return true;
		}

		private bool IsDoingGrabbingPanelGesture()
		{
			return _triggeredInputList.Contains(Convert.ToInt32(EAllowedInputs.GRAB_RIGHT)) || _triggeredInputList.Contains(Convert.ToInt32(EAllowedInputs.GRAB_LEFT));
		}

		private bool IsDoingGrabbingPanelGestureLeft()
		{
			return _triggeredInputList.Contains(Convert.ToInt32(EAllowedInputs.GRAB_LEFT));
		}

		private bool IsDoingGrabbingPanelGestureRight()
		{
			return _triggeredInputList.Contains(Convert.ToInt32(EAllowedInputs.GRAB_RIGHT));
		}

		private bool IsDoingGesture()
		{
			return _triggeredInputList.Count > 0;
		}

		private Vector3 GetOneHandedControllerPosition()
		{
			if (IsLeftControllerOn())
			{
				return _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
			}
			return _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
		}

		private float CurrentHandsDistance(bool openingPhase)
		{
			if (IsOneHanded())
				return openingPhase
					? 0.001f //at this stage, _oneHandedOrigin isn't set yet, so we basically just assume that left and right hands are together
					: Vector3.Distance(_oneHandedOrigin, GetOneHandedControllerPosition());

			var l = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
			var r = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
			return Vector3.Distance(l, r);
		}

		private void HandleInput(bool value, UdonInputEventArgs args)
		{
			if (IsDoingOpeningPanelGesture() 
				&& (!IsPanelOpen() || !IsOneHanded()) //No scaling in one handed mode, because it prevents the panel from being grabbed
				&& Mathf.Abs(_timeStartTrigger - _timeEndTrigger) < TIME_INTERVAL_HAND_GESTURE)
			{
				var dist = CurrentHandsDistance(true);

				if (dist >= ScaleValueToAvatar(MAX_DISTANCE_HAND_GESTURE) && !IsPanelOpen()) return;

				//If the grab gesture is used on both hands, we open the panel
				//but if the panel was already open, we rescale the panel
				OnPanelGrab();

				if (!IsPanelOpen() && _forceStateOfPanel != EForceState.FORCE_CLOSE)
				{
					_startDistanceBetweenTwoHands = dist;
					_oneHandedOrigin = GetOneHandedControllerPosition();
					_startScale = dist;
					OpenPanel();
				}
				else
				{
					_oneHandedOrigin = _panelTransf.transform.position;
					_startScale = _panelTransf.localScale.x;
					_startDistanceBetweenTwoHands = Vector3.Distance(_oneHandedOrigin, GetOneHandedControllerPosition());
				}

				_grabbed = EGrabbed.BOTH_HANDS;
				return;
			}

			if (!IsDoingGesture())
			{
				//No more gestures => Dropping the panel
				_forceStateOfPanel = EForceState.NONE;

				OnPanelDrop();

				if (IsPanelOpen() && _panelTransf.localScale.x < ScaleValueToAvatar(CLOSING_HAND_DISTANCE))
				{
					CloseOrRespawnPanel();
				}

				_grabbed = EGrabbed.NONE;
				return;
			}

			if (IsDoingGrabbingPanelGesture())
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

				if (_pickupModule == null && IsPanelOpen())
				{
					AttachToHand();
				}
			}
		}

		private void HandleInputGrab(bool value, UdonInputEventArgs args)
		{
			if (!_localPlayer.IsUserInVR())
			{
				return;
			}

			UpdateTriggeredInputList(
				args.handType == HandType.LEFT ? EAllowedInputs.GRAB_LEFT : EAllowedInputs.GRAB_RIGHT,
				value
			);

			HandleInput(value, args);
		}

		private bool IsViveController()
		{
			string[] joystickNames = UnityEngine.Input.GetJoystickNames();
			foreach(var joystickName in joystickNames)
			{
				if (joystickName.ToLower().Contains("vive"))
					return true;
			}
			return false;
		}

		public override void InputDrop(bool value, UdonInputEventArgs args)
		{
			if (!_init || !_isUsingViveControllers) return;

			HandleInputGrab(value, args);
		}

		public override void InputGrab(bool value, UdonInputEventArgs args)
		{
			if (!_init || _isUsingViveControllers) return;

			HandleInputGrab(value, args);
		}

		public override void InputUse(bool value, UdonInputEventArgs args)
		{
			if (!_init) return;

			if (!_localPlayer.IsUserInVR())
			{
				return;
			}

			UpdateTriggeredInputList(
				args.handType == HandType.LEFT ? EAllowedInputs.TRIGGER_LEFT : EAllowedInputs.TRIGGER_RIGHT,
				value
			);

			HandleInput(value, args);
		}

		private void EventEnableOneHandMovement()
		{
			if (_grabbed == EGrabbed.BOTH_HANDS)
			{
				AttachToHand();
			}
			_eventAlreadySend = false;
		}

		private bool PanelTooFarAway()
		{
			return Vector3.Distance(_localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position, _panelTransf.position)
				 > ScaleValueToAvatar(MaxDistanceBeforeClosingThePanel);
		}

		private void AttachToHand()
		{
			if (_pickupModule != null)
			{
				return;
			}
			else
			{
				_isPanelOpen = true;
				_grabbed = EGrabbed.ONE_HANDED;

				if (IsDoingGrabbingPanelGestureLeft())
					_panelAttachedToHand = VRCPlayerApi.TrackingDataType.LeftHand;
				else
					_panelAttachedToHand = VRCPlayerApi.TrackingDataType.RightHand;

				VRCPlayerApi.TrackingData hand = _localPlayer.GetTrackingData(_panelAttachedToHand);
				_offsetRotation = Quaternion.Inverse(hand.rotation) * _panelTransf.rotation;
				_offsetPosition = Quaternion.Inverse(hand.rotation) * (_panelTransf.position - hand.position);

				OnPanelGrab();
			}
		}

		public void _PanelPickedUp()
		{
			_isPanelOpen = true;
			_grabbed = EGrabbed.ONE_HANDED;
			_startScale = _panelTransf.localScale.x;
			_currentScale = _startScale;
			OnPanelGrab();
		}

		public void _PanelDropped()
		{
			_grabbed = EGrabbed.NONE;
			OnPanelDrop();
		}

		public override void PostLateUpdate()
		{
			if (!_init) return;

			if (!_localPlayer.IsUserInVR())
			{
				if (TabOnHold)
				{
					bool tabPressed = UnityEngine.Input.GetKey(KeyCode.Tab);

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
							CloseOrRespawnPanel();
						}
						if (!tabPressed)
						{
							_forceStateOfPanel = EForceState.NONE;
						}
					}
				}
				else
				{
					bool tabPressedDown = UnityEngine.Input.GetKeyDown(KeyCode.Tab);

					if (!IsPanelOpen() && (tabPressedDown || _forceStateOfPanel == EForceState.FORCE_OPEN))
					{
						OpenPanel();
						PlacePanelInFrontOfPlayer();

						_forceStateOfPanel = EForceState.NONE;
					}
					else if (IsPanelOpen() && (tabPressedDown || _forceStateOfPanel == EForceState.FORCE_CLOSE || PanelTooFarAway()))
					{
						CloseOrRespawnPanel();
						_forceStateOfPanel = EForceState.NONE;
					}
				}
			}
			else
			{
				if (_grabbed == EGrabbed.NONE)
				{
					if (IsPanelOpen() && PanelTooFarAway())
					{
						CloseOrRespawnPanel();
					}
					return;
				}
				else if (_grabbed == EGrabbed.ONE_HANDED)
				{
					if (_isPickupable && !_pickupModule)
					{
						VRCPlayerApi.TrackingData hand = _localPlayer.GetTrackingData(_panelAttachedToHand);
						SetOwner();
						_panelTransf.position = (hand.position + (hand.rotation * _offsetPosition));
						_panelTransf.rotation = (hand.rotation * _offsetRotation);
					}
				}
				else
				{
					float distance = CurrentHandsDistance(false);
					Vector3 origin;
					if (IsOneHanded())
					{
						distance *= 2.0f; //distance between the two extremities of the panel = radius * 2
						origin = _oneHandedOrigin;
					}
					else
					{
						Vector3 left = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
						Vector3 right = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
						origin = ((left + right) / 2.0f);
					}
					Vector3 headPos = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

					_currentScale = Mathf.Clamp(_startScale * distance / _startDistanceBetweenTwoHands, ScaleValueToAvatar(MinScale), ScaleValueToAvatar(MaxScale));
					SetOwner();
					_panelTransf.position = origin;
					SetPanelScale(_currentScale);
					_panelTransf.LookAt(headPos);
					_panelTransf.forward = -_panelTransf.forward;
				}
			}
		}

		private void PlacePanelInFrontOfPlayer(float unscaledDistance = PLACEMENT_DISTANCE_FROM_HEAD)
		{
			Quaternion headRot = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
			Vector3 headPos = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

			float distance = ScaleValueToAvatar(unscaledDistance);
			float scale = ScaleValueToAvatar(PanelScaleOnDesktop) * unscaledDistance / PLACEMENT_DISTANCE_FROM_HEAD;
			if (distance < 0.08f)
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
			SetOwner();
			_panelTransf.position = (headPos + headRot * Vector3.forward * distance);
			SetPanelScale(scale);
			_panelTransf.LookAt(headPos);
			_panelTransf.forward = -_panelTransf.forward;
		}

		#endregion
	}
}
