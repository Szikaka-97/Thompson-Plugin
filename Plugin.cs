using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Receiver2;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using R2CustomSounds;

namespace Thompson {
	[BepInPlugin("pl.szikaka.thompson", "Thompson Plugin", "1.0.0")]
	public class ThompsonCorePlugin : BaseUnityPlugin {
		
		private static readonly int gun_model = 1005;
		private static ThompsonCorePlugin instance;

		private static FieldInfo reload_pose_spring;
		private static MethodInfo tryFireBullet;

		private static ConfigEntry<bool> use_custom_sounds;

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

		private static Dictionary<string, string> customEvents = new() {
			{ "fire", "event:/guns/1911/shot"},
			{ "dry_fire", "custom:/thompson/bolt_drop"},
			{ "safety", "custom:/thompson/safety_lever"},
			{ "bolt_back", "custom:/thompson/bolt_back"},
			{ "bolt_back_partial", "custom:/thompson/bolt_back_partial"},
			{ "trigger_reset", "custom:/thompson/trigger_release"}
		};

		private static Dictionary<string, string> defaultEvents = new() {
			{ "fire", "event:/guns/1911/shot"},
			{ "dry_fire", "event:/guns/hipoint/slide_released"},
			{ "safety", "event:/guns/deagle/safety_off"},
			{ "bolt_back", "event:/guns/1911/1911_slide_back"},
			{ "bolt_back_partial", "event:/guns/1911/1911_slide_back"},
			{ "trigger_reset", "event:/guns/1911/1911_trigger_reset"}
		};

		private static void setSoundEvents(ref GunScript gun, Dictionary<string, string> events) {
			gun.sound_event_gunshot = events["fire"];
			gun.sound_dry_fire = events["dry_fire"];
			gun.sound_safety_off = events["safety"];
			gun.sound_safety_on = events["safety"];
			gun.sound_slide_back = events["bolt_back"];
			gun.sound_slide_back_partial = events["bolt_back_partial"];
			gun.sound_trigger_reset = events["trigger_reset"];
		}

		private static float InterpCurve(in float[] curve, float t) { //Copied from the dll
			if (t <= curve[0])
			{
				return curve[1];
			}
			if (t >= curve[curve.Length - 2])
			{
				return curve[curve.Length - 1];
			}
			for (int i = 0; i < curve.Length - 2; i += 2)
			{
				if (t < curve[i + 2])
				{
					float num = (t - curve[i]) / (curve[i + 2] - curve[i]);
					return Mathf.Lerp(curve[i + 1], curve[i + 3], num);
				}
			}
			throw new Exception("Error in InterpCurve");
		}

		private static System.Collections.IEnumerator fireRound(GunScript script) { //Firing is delayed one frame to allow the magazine to transfer round to the chamber
			yield return null;

			tryFireBullet.Invoke(script, new object[] { 1f });
		}

		private void Awake() {
			Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

			reload_pose_spring = typeof(LocalAimHandler).GetField("reload_pose_spring", BindingFlags.Instance | BindingFlags.NonPublic);
			tryFireBullet = typeof(GunScript).GetMethod("TryFireBullet", BindingFlags.Instance | BindingFlags.NonPublic);

			instance = this;

			Harmony.CreateAndPatchAll(GetType());

			ModAudioManager.LoadCustomEvents("thompson", Application.persistentDataPath + "/Guns/Thompson/Sounds");

			use_custom_sounds = Config.Bind("Gun Settimgs", "Use custom sounds", true);
		}

		[HarmonyPatch(typeof(ReceiverCoreScript), "Awake")]
		[HarmonyPostfix]
		private static void patchCoreAwake(ref ReceiverCoreScript __instance, ref GameObject[] ___gun_prefabs_all, ref List<MagazineScript> ___magazine_prefabs_all) {
			GameObject thompson = null;
			MagazineScript thompson_mag = null;

			try {
				thompson = ___gun_prefabs_all.First( go => (int) go.GetComponent<GunScript>().gun_model == gun_model );

				thompson_mag = ___magazine_prefabs_all.First( ms => (int) ms.gun_model == gun_model );
			} catch (Exception e) {
				Debug.LogError("Couldn't load gun \"Thompson M1928A1\"");
				Debug.Log(e.StackTrace);
				return;
			}


			thompson.GetComponent<GunScript>().loaded_cartridge_prefab = __instance.generic_prefabs.First( p => p is ShellCasingScript && ((ShellCasingScript) p).cartridge_type == CartridgeSpec.Preset._45_acp ).gameObject;

			thompson.GetComponent<GunScript>().pooled_muzzle_flash = ___gun_prefabs_all.First(go => go.GetComponent<GunScript>().gun_model == GunModel.Model10).GetComponent<GunScript>().pooled_muzzle_flash;

			thompson_mag.round_prefab = thompson.GetComponent<GunScript>().loaded_cartridge_prefab;

			Debug.Log("Thompson loaded");

			__instance.generic_prefabs = new List<InventoryItem>(__instance.generic_prefabs) {
				thompson.GetComponent<GunScript>(),
				thompson_mag
			}.ToArray();

			LocaleTactics lt = new LocaleTactics();

			lt.gun_internal_name = "thompson.thompson_m1928_a1";
			lt.title = "Thompson M1928A1";
			lt.text = "A modded submachinegun.\n" +
				"Developed after World War 1, the Thompson submachinegun has proven itself to be a huge success, both in its sales and presence in the media. It soon rose to fame during the american prohibition period, used both by forces of law and the criminal underground, securing it's position as an iconic \"gangster weapon\"";

			Locale.active_locale_tactics.Add("thompson.thompson_m1928_a1", lt);

			__instance.PlayerData.unlocked_gun_names.Add("thompson.thompson_m1928_a1");

		}

		[HarmonyPatch(typeof(GunScript), nameof(GunScript.Awake))]
		[HarmonyPostfix]
		private static void patchGunAwake(ref GunScript __instance) {
			if ((int) __instance.gun_model != gun_model) return;

			var properties = __instance.gameObject.AddComponent<ThompsonWeaponProperties>();

			properties.selector.transform = __instance.transform.Find("selector");
			properties.selector.rotations[0] = __instance.transform.Find("selector_full_auto").rotation;
			properties.selector.rotations[1] = __instance.transform.Find("selector_semi_auto").rotation;

			properties.sear.transform = __instance.transform.Find("sear");
			properties.sear.rotations[0] = __instance.slide_stop.rotations[0];
			properties.sear.rotations[1] = __instance.slide_stop.rotations[1];

			properties.disconnector_lever.transform = __instance.trigger.transform.Find("disconnector_lever");
			__instance.ApplyTransform("disconnector_lever", 0, properties.disconnector_lever.transform);
			properties.disconnector_lever.rotations[0] = properties.disconnector_lever.transform.localRotation;
			__instance.ApplyTransform("disconnector_lever", 1, properties.disconnector_lever.transform);
			properties.disconnector_lever.rotations[1] = properties.disconnector_lever.transform.localRotation;

			properties.recoil_spring = __instance.transform.Find("spring_recoil").GetComponent<SkinnedMeshRenderer>();

			properties.trigger_spring = __instance.transform.Find("spring_trigger").GetComponentInChildren<SkinnedMeshRenderer>();
		}

		[HarmonyPatch(typeof(GunScript), nameof(GunScript.Update))]
		[HarmonyPostfix]
		private static void patchGunUpdate(ref GunScript __instance, ref bool ___slide_stop_locked, ref bool ___disconnector_needs_reset) {
			if ((int) __instance.gun_model != gun_model || Time.timeScale == 0 || !__instance.enabled || __instance.GetHoldingPlayer() == null || LocalAimHandler.player_instance.hands[1].state != LocalAimHandler.Hand.State.HoldingGun) return;

			LocalAimHandler lah = LocalAimHandler.player_instance;
			ThompsonWeaponProperties properties = __instance.GetComponent<ThompsonWeaponProperties>();

			setSoundEvents(ref __instance, use_custom_sounds.Value ? customEvents : defaultEvents);

			___slide_stop_locked = true;

			__instance.has_thumb_safety = __instance.slide.amount >= __instance.slide_lock_position; 

			if (__instance.IsSafetyOn()) {
				((Spring) reload_pose_spring.GetValue(lah)).target_state = 0.55f;
				((Spring) reload_pose_spring.GetValue(lah)).Update(Time.deltaTime);
			}

			var magazine = __instance.magazine_instance_in_gun;
			bool trigger_engaging_sear = !___disconnector_needs_reset && !(magazine != null && magazine.NumRounds() == 0 && magazine.press_amount == 0);

			if (
				lah.character_input.GetButtonDown(14) &&
				__instance.slide.amount >= __instance.slide_lock_position &&
				__instance.trigger.amount == 0
			) {
				properties.selector.asleep = false;
				if (properties.selector.target_amount == 1) {
					properties.selector.target_amount = 0;
					properties.selector.accel = -1;
					properties.selector.vel = -10;
				}
				else {
					properties.selector.target_amount = 1;
					properties.selector.accel = 10;
					properties.selector.vel = 10;
				}

				AudioManager.PlayOneShotAttached(__instance.sound_safety_on, properties.selector.transform.gameObject);
			}
			
			if (__instance.IsSafetyOn()) {
				__instance.trigger.amount = Math.Min(__instance.trigger.amount, 0.1f);
				__instance.trigger.UpdateDisplay();
			}


			__instance.ApplyTransform("lever_sear", __instance.trigger.amount * Convert.ToInt32(trigger_engaging_sear), __instance.transform.Find("lever_sear"));

			__instance.ApplyTransform("disconnector_trip", properties.selector.amount, __instance.transform.Find("disconnector_trip"));

			if (magazine != null && magazine.NumRounds() == 0 && magazine.press_amount == 0) {
				properties.disconnector_lever.amount = InterpCurve(disconnector_lever_magazine_trip_curve, __instance.trigger.amount);
				__instance.ApplyTransform("magazine_trip", 1, __instance.transform.Find("magazine_trip"));
			}
			else __instance.ApplyTransform("magazine_trip", 0, __instance.transform.Find("magazine_trip"));

			properties.disconnector_lever.UpdateDisplay();
			if (properties.selector.amount == 1) {
				__instance.ApplyTransform("disconnector_trip_rotation", __instance.slide.amount, __instance.transform.Find("disconnector_trip"));

				if (__instance.trigger.amount != 0) {
					properties.disconnector_lever.amount = Mathf.Max(Mathf.InverseLerp(0.1112f, 0, __instance.slide.amount), properties.disconnector_lever.amount);
				}
				else {
					properties.disconnector_lever.amount = Mathf.MoveTowards(properties.disconnector_lever.amount, 0, Time.deltaTime * 20);
				}
			}
			else {
				properties.disconnector_lever.amount = Mathf.MoveTowards(properties.disconnector_lever.amount, 0, Time.deltaTime * 20);
			}

			if (
				__instance.trigger.amount == 1 &&
				!__instance.IsSafetyOn() &&
				!(__instance.magazine_instance_in_gun != null && __instance.magazine_instance_in_gun.IsEmpty()) &&
				!(properties.selector.amount != 0 && ___disconnector_needs_reset)
			){
				__instance.slide_stop.target_amount = 0;
			}
			else if (__instance.slide.amount >= __instance.slide_lock_position) {
				__instance.slide_stop.target_amount = 1;
			}
			if (__instance.trigger.amount == 0) {
				if (___disconnector_needs_reset) AudioManager.PlayOneShotAttached(__instance.sound_trigger_reset, __instance.gameObject);
				___disconnector_needs_reset = false;
			}

			if (__instance.slide.amount == 0 && properties.previous_slide_amount > 0) {
				instance.StartCoroutine(fireRound(__instance));

				___disconnector_needs_reset = properties.selector.amount != 0;
			}

			properties.previous_slide_amount = __instance.slide.amount;

			foreach (var component in __instance.animated_components) {
				__instance.ApplyTransform(
					component.anim_path,
					component.mover.amount,
					component.component
				);
			}
			__instance.safety.UpdateDisplay();
			properties.selector.TimeStep(Time.deltaTime);

			properties.sear.amount = Mathf.Max(__instance.trigger.amount * Convert.ToSingle(trigger_engaging_sear), InterpCurve(sear_curve, __instance.slide.amount));
			properties.sear.UpdateDisplay();

			properties.recoil_spring.SetBlendShapeWeight(0, (1 - __instance.slide.amount) * 100);
			properties.trigger_spring.SetBlendShapeWeight(0, __instance.trigger.amount * 100);
		}

		[HarmonyPatch(typeof(MagazineScript), nameof(MagazineScript.UpdateRoundPositions))]
		[HarmonyPostfix]
		private static void patchMagUpdatePositions(ref MagazineScript __instance) {
			if ((int) __instance.gun_model != gun_model || __instance.rounds.Count == 0) return;
						
			var round_transform = __instance.rounds[0].transform;

			if (__instance.rounds.Count % 2 == 0) {
				if (round_transform.localPosition.x > -0.0049f)
					round_transform.localPosition += new Vector3(-0.0049f, 0);
			}
			else {
				if (round_transform.localPosition.x < 0.0049f)
				round_transform.localPosition += new Vector3(0.0049f, 0);
			}

			__instance.transform.Find("magazine_spring").GetComponent<SkinnedMeshRenderer>().SetBlendShapeWeight(0, 
				Mathf.InverseLerp(0, 0.09470001f, Vector3.Distance(__instance.follower.localPosition, __instance.transform.Find("follower_under_round_bottom").localPosition)) * 100
			);
		}
	}
}
