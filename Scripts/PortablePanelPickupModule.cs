
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace myro
{
	public class PortablePanelPickupModule : UdonSharpBehaviour
	{
		[Header("See README file for additional help and infos")]
		[Header("This script should be attached next to the VRCPickup component")]
		public PortablePanel PortablePanelReference;
		private VRCPickup _pickupReference;
		
		void OnEnable()
		{
			if (PortablePanelReference)
				PortablePanelReference.SetPickupModule(this);

			_pickupReference = GetComponent<VRCPickup>();
		}

		public override void OnPickup()
		{
			if (PortablePanelReference)
				PortablePanelReference.PanelPickedUp();
		}

		public override void OnDrop()
		{
			if (PortablePanelReference)
				PortablePanelReference.PanelDropped();
		}

		public void DisablePickup()
		{
			if (_pickupReference)
			{
				_pickupReference.pickupable = false;
			}
		}

		public void EnablePickup()
		{
			if (_pickupReference)
			{
				_pickupReference.pickupable = true;
			}
		}
	}
}
