using UnityEngine;
using Receiver2;
using RewiredConsts;
using Receiver2ModdingKit;

namespace Thompson {
	internal class ThompsonScript : ModGunScript {
		public SkinnedMeshRenderer recoil_spring;
		public SkinnedMeshRenderer trigger_spring;

		private float previous_slide_amount;
		private Spring levers_inspect_pose_spring = new Spring(0, 0, 90, 1E-06f);
		private RotateMover selector = new RotateMover();
		private RotateMover sear = new RotateMover();
		private RotateMover disconnector_lever = new RotateMover();
		private Transform pull_back_slide_pose;
		private Transform pull_back_slide_original_pose;
		private Transform inspect_levers_pose;

		private static float[] sear_curve = new float[] {
			0,
			1,
			0.8f,
			1,
			0.868f,
			0,
			0.95f,
			0.8f,
			1,
			0.2f
		};

		private static float[] disconnector_lever_magazine_trip_curve = new float[] { //Long ass name lol
			0,
			0,
			0.3f,
			1
		};

		public override void InitializeGun() {
			CorePlugin.modding_kit_present = true; // Will be triggered if the modding kit loads the gun
		}

		public override LocaleTactics GetGunTactics() {
			return new LocaleTactics() {
				gun_internal_name = "thompson.thompson_m1928_a1",
				title = "Thompson M1928A1",
				text = "A modded submachinegun.\nDeveloped after World War 1, the Thompson submachinegun has proven itself to be a huge success, both in its sales and presence in the media. It soon rose to fame during the american prohibition period, used both by forces of law and the criminal underground, securing it's position as an iconic \"gangster weapon\""
			};
		}

		public override void AwakeGun() {
			this.selector.transform = this.transform.Find("selector");
			this.selector.rotations[0] = this.transform.Find("selector_full_auto").rotation;
			this.selector.rotations[1] = this.transform.Find("selector_semi_auto").rotation;

			this.sear.transform = this.transform.Find("sear");
			this.sear.rotations[0] = this.slide_stop.rotations[0];
			this.sear.rotations[1] = this.slide_stop.rotations[1];

			this.disconnector_lever.transform = this.trigger.transform.Find("disconnector_lever");
			this.ApplyTransform("disconnector_lever", 0, this.disconnector_lever.transform);
			this.disconnector_lever.rotations[0] = this.disconnector_lever.transform.localRotation;
			this.ApplyTransform("disconnector_lever", 1, this.disconnector_lever.transform);
			this.disconnector_lever.rotations[1] = this.disconnector_lever.transform.localRotation;

			this.recoil_spring = this.transform.Find("spring_recoil").GetComponent<SkinnedMeshRenderer>();

			this.trigger_spring = this.transform.Find("spring_trigger").GetComponentInChildren<SkinnedMeshRenderer>();

			this.pull_back_slide_pose = this.transform.Find("pose_slide_pull");
			this.pull_back_slide_original_pose = this.transform.Find("pose_slide_pull_original");
			this.inspect_levers_pose = this.transform.Find("pose_inspect_levers");
		}

		public override void UpdateGun() {
			_slide_stop_locked = true;

			this.has_thumb_safety = this.slide.amount >= this.slide_lock_position; 

			if (player_input.GetButton(Action.Slide_Lock) && this.slide.amount > this.slide_lock_position) {
				levers_inspect_pose_spring.target_state = 1f;
			}
			else {
				levers_inspect_pose_spring.target_state = 0f;
			}

			levers_inspect_pose_spring.Update(Time.deltaTime);

			this.pull_back_slide_pose.localRotation = Quaternion.LerpUnclamped(pull_back_slide_original_pose.localRotation, inspect_levers_pose.localRotation, levers_inspect_pose_spring.state);
			this.pull_back_slide_pose.localPosition = Vector3.LerpUnclamped(pull_back_slide_original_pose.localPosition, inspect_levers_pose.localPosition, levers_inspect_pose_spring.state);

			var magazine = this.magazine_instance_in_gun;
			bool trigger_engaging_sear = !_disconnector_needs_reset && !(magazine != null && magazine.NumRounds() == 0 && magazine.press_amount == 0);

			if (
				player_input.GetButtonDown(Action.Hammer) &&
				this.slide.amount >= this.slide_lock_position &&
				this.trigger.amount == 0
			) {
				this.selector.asleep = false;
				if (this.selector.target_amount == 1) {
					this.selector.target_amount = 0;
					this.selector.accel = -1;
					this.selector.vel = -10;
				}
				else {
					this.selector.target_amount = 1;
					this.selector.accel = 10;
					this.selector.vel = 10;
				}

				AudioManager.PlayOneShotAttached(this.sound_safety_on, this.selector.transform.gameObject);
			}
			
			if (this.IsSafetyOn()) {
				this.trigger.amount = Mathf.Min(this.trigger.amount, 0.1f);
				this.trigger.UpdateDisplay();
			}

			this.ApplyTransform("lever_sear", this.trigger.amount * (trigger_engaging_sear ? 1 : 0), this.transform.Find("lever_sear"));

			this.ApplyTransform("disconnector_trip", this.selector.amount, this.transform.Find("disconnector_trip"));

			if (magazine != null && magazine.NumRounds() == 0 && magazine.press_amount == 0) {
				this.disconnector_lever.amount = InterpCurve(disconnector_lever_magazine_trip_curve, this.trigger.amount);
				this.ApplyTransform("magazine_trip", 1, this.transform.Find("magazine_trip"));
			}
			else this.ApplyTransform("magazine_trip", 0, this.transform.Find("magazine_trip"));

			this.disconnector_lever.UpdateDisplay();
			if (this.selector.amount == 1) {
				this.ApplyTransform("disconnector_trip_rotation", this.slide.amount, this.transform.Find("disconnector_trip"));

				if (this.trigger.amount != 0) {
					this.disconnector_lever.amount = Mathf.Max(Mathf.InverseLerp(0.1112f, 0, this.slide.amount), this.disconnector_lever.amount);
				}
				else {
					this.disconnector_lever.amount = Mathf.MoveTowards(this.disconnector_lever.amount, 0, Time.deltaTime * 20);
				}
			}
			else {
				this.disconnector_lever.amount = Mathf.MoveTowards(this.disconnector_lever.amount, 0, Time.deltaTime * 20);
			}

			if (
				this.trigger.amount == 1 &&
				!this.IsSafetyOn() &&
				!(this.magazine_instance_in_gun != null && this.magazine_instance_in_gun.IsEmpty()) &&
				!(this.selector.amount != 0 && _disconnector_needs_reset)
			){
				this.slide_stop.target_amount = 0;
			}
			else if (this.slide.amount >= this.slide_lock_position) {
				this.slide_stop.target_amount = 1;
			}
			if (this.trigger.amount == 0) {
				if (_disconnector_needs_reset) AudioManager.PlayOneShotAttached(this.sound_trigger_reset, this.gameObject);
				_disconnector_needs_reset = false;
			}

			if (this.slide.amount == 0 && this.previous_slide_amount > 0) {
				this.TryFireBullet(1);

				_disconnector_needs_reset = this.selector.amount != 0;
			}

			this.previous_slide_amount = this.slide.amount;

			this.safety.UpdateDisplay();
			this.selector.TimeStep(Time.deltaTime);

			this.sear.amount = Mathf.Max(this.trigger.amount * (trigger_engaging_sear ? 1 : 0), InterpCurve(sear_curve, this.slide.amount));
			this.sear.UpdateDisplay();

			this.recoil_spring.SetBlendShapeWeight(0, (1 - this.slide.amount) * 100);
			this.trigger_spring.SetBlendShapeWeight(0, this.trigger.amount * 100);

			this.UpdateAnimatedComponents();
		}
	}
}
