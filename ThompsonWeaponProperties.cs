using UnityEngine;
using Receiver2;

namespace Thompson {
	class ThompsonWeaponProperties : MonoBehaviour {
		public float previous_slide_amount;
		public RotateMover selector = new RotateMover();
		public RotateMover sear = new RotateMover();
		public RotateMover disconnector_lever = new RotateMover();
		public SkinnedMeshRenderer recoil_spring;
		public SkinnedMeshRenderer trigger_spring;
	}
}
