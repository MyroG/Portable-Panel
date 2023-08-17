
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// Here's a script example of a panel connected to a rigid body
/// The idea is to be able to throw the panel away, and once it's further away from the player, it will disintegrate
/// </summary>
public class PortablePanelWithRigidBody : myro.PortablePanel
{
	public Rigidbody PanelRigidBody;
    public ParticleSystem ClosingParticleAnimation;
	public BoxCollider PickupCollider;

	private Rigidbody _particleSystemRB;

	public override void OnStart()
    {
		_particleSystemRB = ClosingParticleAnimation.GetComponent<Rigidbody>();

		if (!Networking.LocalPlayer.IsUserInVR())
		{
			ClosingParticleAnimation.gameObject.SetActive(false);
			PickupCollider.enabled = false;
			_particleSystemRB.isKinematic = true;
		}
	}

	public override void OnPanelGrab()
	{
		//to make sure the panel doesn't uncontrolably fly away when the panel gets dropped, we will set the velocity to 0 once the panel is grabbed
		PanelRigidBody.velocity = Vector3.zero;
		PanelRigidBody.angularVelocity = Vector3.zero;
	}

	public override bool OnPanelClosing()
	{
		//once the panel closes, let's just play a basic disintegration animation
		//we first place the particle emitter at the exact location the panel was
		ClosingParticleAnimation.transform.position = Panel.transform.position;
		ClosingParticleAnimation.transform.rotation = Panel.transform.rotation;
		ClosingParticleAnimation.transform.localScale = Panel.transform.localScale;

		//momentum transfer
		if (_particleSystemRB != null)
		{
			_particleSystemRB.velocity = PanelRigidBody.velocity;
			_particleSystemRB.angularVelocity = PanelRigidBody.angularVelocity;
		}

		//playing the animation
		ClosingParticleAnimation.Stop();
		ClosingParticleAnimation.Play();

		return true; //We want to close the panel
	}
}
