
using myro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class GrabbableToggle : UdonSharpBehaviour
{
	public Material IsOn;
	public Material IsOff;
	
	public PortablePanel[] portablePanels;

	private MeshRenderer _renderer;
	
    void Start()
    {
		_renderer = GetComponent<MeshRenderer>();

	}

	public override void Interact()
	{
		SetToggleState();
	}

	private  void SetToggleState()
	{
		foreach (var panel in portablePanels)
		{
			panel._TogglePickupable();
		}

		_renderer.material = portablePanels[0].IsPickupable() ? IsOn : IsOff;
	}
}
