
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
		public EGestureMode GestureMode;
		public bool GrabbablePanel = true;
		public float MaxScale = 9999.0f;
		public float MinScale = 0.1f;
		public float MaxDistanceBeforeClosingThePanel = 2f;
		public float PanelScaleOnDesktop = 0.5f;

		private VRCPlayerApi _localPlayer;
		private EGrabbed _grabbed;
		private bool _isRightHandTriggered , _isLeftHandTriggered;
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

		void OnEnable()
		{
			_localPlayer = Networking.LocalPlayer;
			ClosePanel();
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
			return Panel.activeSelf;
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
			_isRightHandTriggered = false;
			_isLeftHandTriggered = false;

			if (OnPanelClosing())
			{
				Panel.SetActive(false);
			}
		}

		private void OpenPanel()
		{
			Panel.SetActive(true);
			OnPanelOpening();
		}

		private void HandleInput(bool value, UdonInputEventArgs args)
		{
			if (args.handType == HandType.RIGHT)
			{
				_isRightHandTriggered = value;
				_timeRightHandGesture = Time.time;
			}
			else if (args.handType == HandType.LEFT)
			{
				_isLeftHandTriggered = value;
				_timeLeftHandGesture = Time.time;
			}

			if (_isRightHandTriggered && _isLeftHandTriggered 
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
			else if (!_isRightHandTriggered && !_isLeftHandTriggered)
			{
				//If the panel is not grabbed anymore, we close the panel under two conditions:
				//- If the panel became twice as small
				//- If the panel is smaller than "MinScale"
				_forceStateOfPanel = EForceState.NONE;

				OnPanelDrop();

				if (IsPanelOpen() && (
					   _startScale / 2.0f > _currentScale
					|| _currentScale < MinScale)
				)
				{
					ClosePanel();
				}
				_grabbed = EGrabbed.NONE;
			}
			else if (_isRightHandTriggered || _isLeftHandTriggered)
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

			if (!GrabbablePanel)
			{
				_grabbed = EGrabbed.NONE;
				return;
			}
			else
			{
				_grabbed = EGrabbed.ONE_HANDED;

				if (_isLeftHandTriggered)
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
				Vector3 headPos = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

				if (_grabbed == EGrabbed.NONE)
				{
					if (IsPanelOpen() && Vector3.Distance(headPos, Panel.transform.position) > MaxDistanceBeforeClosingThePanel)
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
		}

		public override void PostLateUpdate()
		{
			//On Desktop, it is more interesting to use PostLateUpdate, so the panel doesn't lag behind.
			if (!_localPlayer.IsUserInVR())
			{
				bool tabPressed = Input.GetKey(KeyCode.Tab);

				if ((tabPressed && _forceStateOfPanel != EForceState.FORCE_CLOSE) || _forceStateOfPanel == EForceState.FORCE_OPEN)
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
		}

		private void PlacePanelInFrontOfPlayer()
		{
			Quaternion headRot = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
			Vector3 headPos = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

			Panel.transform.position = (headPos + headRot * Vector3.forward * .3f);
			SetPanelScale(PanelScaleOnDesktop);
			Panel.transform.LookAt(headPos);
			Panel.transform.forward = -Panel.transform.forward;
		}

		#endregion
	}
}
