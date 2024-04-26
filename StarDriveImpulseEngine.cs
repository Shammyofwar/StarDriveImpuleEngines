using KSP.UI.Screens;
using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using System.Text;




//Code by Shammyofwar 2024 - -Original Source by Shadow1138 released under GPL3.0

namespace StarDriveImpulseEngine
{


    //###################################################################################################
    // Impulse Engine
    // Uses  Liquid Dilithium to provide sub-light propulsion 
    //###################################################################################################
    public class Star_ImpulseEngine : ModuleEngines
	{
		[KSPField]
		public float maxThrust = 0f;

		[KSPField]
		public float maxAccel = 2.0f;           //This is in units of G

		[KSPField]
		public float maxVerticalAccel = 1.0f;   //This is in units of m/s^2

		[KSPField]
		public float hoverAlt = 500f;           //This is in units of meters

		[KSPField]
		public float engineEfficiency = 0.75f;

		[KSPField]
		public float minDilithium = 0.1f;

		[KSPField]
		public float ecProd = 1000;

		[KSPField]
		public float maxVectorAngle = 15f;      // This is in units of degrees

		// The path to the engine sound effect
		[KSPField]
		public string engineSFX = "ScifiShipyardsRedux/Sounds/tng_engine_hum_short";

		// Toggle for the hover mode in the PAW
		[KSPField(groupName = "ImpulsePAW", groupDisplayName = "Impulse Engine Operation", guiName = "Hover Mode", isPersistant = true, guiActive = true), UI_Toggle(disabledText = "Landing", enabledText = "Takeoff")]
		public bool takeOff = true;

		// The KSPAction associated with the hover mode.  This allows the hover mode to be a setable action group.
		[KSPAction(guiName = "Toggle Hover Mode", isPersistent = true)]
		public void ToggleModeAction(KSPActionParam param)
		{
			if (!takeOff)
			{
				takeOff = true;
			}
			else
			{
				takeOff = false;
			}
		}

		// Toggle for the translation mode in the PAW, either forward or reverse.  Default is "true", which is forward, "false" is reverse.
		[KSPField(groupName = "ImpulsePAW", guiName = "Translation Mode", isPersistant = true, guiActive = true), UI_Toggle(disabledText = "Reverse", enabledText = "Forward")]
		public bool thrustFwd = true;

		// The KSPAction associated with the translation mode
		[KSPAction(guiName = "Toggle Translation Mode", isPersistent = true)]
		public void ToggleTanslationAction(KSPActionParam param)
		{
			if (!thrustFwd)
			{
				thrustFwd = true;
			}
			else
			{
				thrustFwd = false;
			}
		}

		// Toggle for the Thrust Vectoring of the impulse engines.  If true, thrust vectoring is enabled.  Thrust vecoring is disabled if false.
		[KSPField(groupName = "ImpulsePAW", guiName = "Thrust Vectoring", isPersistant = true, guiActive = true), UI_Toggle(disabledText = "Disabled", enabledText = "Enabled")]
		public bool thrustVectoring = true;

		// The KSPAction associated with the thrust vectoring activation
		[KSPAction(guiName = "Toggle Thrust Vectoring", isPersistent = true)]
		public void ToggleVectoringAction(KSPActionParam param)
		{
			if (!thrustVectoring)
			{
				thrustVectoring = true;
			}
			else
			{
				thrustVectoring = false;
			}
		}

		// PAW variables to see if ctrlState.pitch/roll/yaw hold the inputs for the same from manual and SAS inputs
		// CONFIRMED - this commented out block can be safely removed at a later date.
		/*[KSPField(groupName = "ImpulsePAW", guiName = "Pitch", isPersistant = true, guiActiveEditor = false, guiActive = true)]
		public float currentPitchInput = 0f;

		[KSPField(groupName = "ImpulsePAW", guiName = "Roll", isPersistant = true, guiActiveEditor = false, guiActive = true)]
		public float currentRollInput = 0f;

		[KSPField(groupName = "ImpulsePAW", guiName = "Yaw", isPersistant = true, guiActiveEditor = false, guiActive = true)]
		public float currentYawInput = 0f;

		[KSPField(groupName = "ImpulsePAW", guiName = "Vessel Size", isPersistant = true, guiActiveEditor = false, guiActive = true)]
		public Vector3 sizeOfVessel;
		*/

		// Initialize the AudioSource for the engine sound effect
		private AudioSource engineSound;
		// Engine ignition boolean so that the sound starts once when the engine is activated
		// and stops only once the engine has been shutdown
		private bool isNewlyStarted;

		// Called when the part is started by Unity
		public override void OnStart(StartState state)
		{
			base.OnStart(state);

			// Initialize the engine sound effect
			if (HighLogic.LoadedSceneIsFlight)
			{
				engineSound = this.part.gameObject.AddComponent<AudioSource>();
				engineSound.clip = GameDatabase.Instance.GetAudioClip(engineSFX);
				engineSound.volume = GameSettings.SHIP_VOLUME;
				//engineHum.audio.volume = 1.0f;

				engineSound.panStereo = 0;
				engineSound.rolloffMode = AudioRolloffMode.Linear;
				engineSound.loop = true;
				engineSound.spatialBlend = 1f;
			}
		}

		// Called when the part is made active
		public override void OnActive()
		{
			// This assumes that the part is made active when beginning flight.
			// Set takeOff to "true".
			// This shouldn't affect landing in an adverse manner.
			if (HighLogic.LoadedSceneIsFlight)
			{
				//takeOff = true;
				base.OnActive();
				isNewlyStarted = true;
			}
		}

		// Define the throttle variable and a variable to hold the current active vessel, and the input values for pitch, roll, and yaw
		private float throttle;
		private Vessel ship;
		private float pitchInput;
		private float rollInput;
		private float yawInput;

		// Variables to control the thrust vectoring
		private float fwdThrust;
		private float pitchThrust;
		private float rollThrust;
		private float yawThrust;
		private Vector3 sizeOfVessel;

		// Deuterium related variables
		private double DTAmnt;
		private double DTMaxAmnt;

		// Get the resource ID for "Dilithium" and for "ElectricCharge"
		public int DTID = PartResourceLibrary.Instance.GetDefinition("Dilithium").id;
		public int ecID = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;

		// Initialize the hover control time variable
		private float hoverTime = 0f;

		// Integer variable to determine the thrust direction
		private int thrustDirection;

		// The acceleration variables from the CFG are in Gs.  These variables will hold the actual acceleration numbers
		//private float maxAccelActual = maxAccel * 9.81f;
		//private float maxVertAccelActual = maxVerticalAccel * 9.81f;

		// This variable and function that holds and determines the number of active impulse engines, respectively, on the ship
		// This is necessary to ensure that the weight cancellation actually cancels the weight of the vessel
		// when there are multiple impulse engines on the vessel.
		private int numActiveEngines = 0;

		private void ActiveImpulse()
		{
			// Loop through all of the parts
			for (int i = 0; i < FlightGlobals.ActiveVessel.parts.Count; i++)
			{
				PartModuleList partModules = FlightGlobals.ActiveVessel.parts[i].Modules;
				for (int j = 0; j < partModules.Count; j++)
				{
					if (partModules[j] is Star_ImpulseEngine && FlightGlobals.ActiveVessel.parts[i].FindModulesImplementing<Star_ImpulseEngine>().First().EngineIgnited)
					{
						numActiveEngines += 1;
						//Debug.Log ("[ScifiShipyardsRedux] Number of Active Impulse Engines: " + numActiveEngines);
					}
				}
			}
		}

		// The FixedUpdate function, the meat of the module
		public void FixedUpdate()
		{

			// Set the "throttle" variable only if we are in flight, and get the connected resource totals for
			// electric charge only if we are in flight as well as the "ship" variable to hold the active vessel.
			if (HighLogic.LoadedSceneIsFlight)
			{
				// Set the "throttle" variable with the current mainThrottle percentage
				throttle = FlightGlobals.ActiveVessel.ctrlState.mainThrottle;

				// Set "ship" to the active vessel to simplify the overall code.
				//ship = FlightGlobals.ActiveVessel;
				ship = this.vessel;

				// Get the amount of  Liquid Dilithium in the vessel.
				this.vessel.GetConnectedResourceTotals(DTID, out DTAmnt, out DTMaxAmnt, true);

				// Set the thrustDirection variable based on the selected mode.
				if (thrustFwd)
				{
					thrustDirection = 1;
				}
				else
				{
					thrustDirection = -1;
				}

				// Set the variables to hold the current input values for pitch, roll, and yaw
				pitchInput = FlightGlobals.ActiveVessel.ctrlState.pitch;
				rollInput = FlightGlobals.ActiveVessel.ctrlState.roll;
				yawInput = FlightGlobals.ActiveVessel.ctrlState.yaw;

				sizeOfVessel = ship.vesselSize;

				// Check for if the engine is shutdown.
				// If this is the case, stop playing the sound and set isNewlyStarted = true
				if (!this.EngineIgnited)
				{
					engineSound.Stop();
					isNewlyStarted = true;
				}

			}

			// The below code needs to execute while the engine is active
			if (HighLogic.LoadedSceneIsFlight && this.EngineIgnited)
			{
				// Generate ElectricCharge to emulate the fusion reactor that is the core of the impulse drive.
				this.vessel.RequestResource(this.part, ecID, -ecProd * TimeWarp.fixedDeltaTime, true);

				// Update the propellant gauge
				this.UpdatePropellantGauge(this.propellants.FirstOrDefault());
				this.UpdatePropellantStatus(true);

				// Start playing the engine sound if the engine is newly activated (staged or activated after shutdown)
				if (isNewlyStarted)
				{
					engineSound.Play();
					isNewlyStarted = false;
				}

				// Set the pitch of the sound based on throttle percentage
				engineSound.pitch = (float)Math.Pow(throttle, 0.1);
			}


			// Takeoff and Landing code is encapsulated in this conditional.
			// This conditional will only evaluate true if the vessel is in a suborbital trajectory.
			// In this manner we prevent continual checks for takeoff and landing, and any unwanted forces while in an orbial situation
			if (HighLogic.LoadedSceneIsFlight && (ship.situation != Vessel.Situations.ORBITING || ship.situation != Vessel.Situations.ESCAPING || ship.situation != Vessel.Situations.DOCKED || ship.situation != Vessel.Situations.PRELAUNCH && ship.radarAltitude <= 2f * hoverAlt))
			{
				// Reset the number of active engines to 0, or else the number will grow and grow.
				numActiveEngines = 0;

				// Call the ActiveImpulse() function to determine how many impulse engines are active on the vessel
				ActiveImpulse();

				// Variable to control the takeoff thrust.
				// This variable will hold the predicted altitude at which the vessel will reach hoverAlt in 60sec
				// given the current vertical speed and radarAlt.  Once this value equals or exceeds hoverAlt
				// then the initial upward thrusting will stop and the vessel should coast to the hoverAlt
				// where it will not fall below while Takeoff is true.
				//float cutoffCondition;

				// Takeoff code
				if (this.EngineIgnited && (ship.radarAltitude < hoverAlt) && takeOff)
				{
					//Debug.Log ("[ScifiShipyardsRedux] Taking off"+Vector3.up);
					// This applies the force from the part on the ship's center-of-mass.
					// It works as expected, I just need to tweak the function to control the force applied.
					// I noted when testing this exact line that once it reaches the hover height, it begins to fall,
					// but the code is slowing it down until it started moving back upwards before striking the ground.
					//this.part.AddForceAtPosition ((-10f*(float)ship.totalMass * ship.graviticAcceleration.normalized), ship.CoM);

					//this.part.AddForceAtPosition (((-1f)*ship.graviticAcceleration.magnitude - 1f - (2f * ship.radarAltitude / hoverAlt))*(float)ship.totalMass*ship.graviticAcceleration.normalized, ship.CoM);

					// Calculate the cutoffCondtion using simple kinematics, ignoring air resistance.
					//cutoffCondition = (hoverAlt - (float)ship.radarAltitude) / ((float)ship.verticalSpeed/(2f*9.81f)) + (0.5f * 9.81f * ((float)ship.verticalSpeed/(2f*9.81f)));

					// If the vessel is below half of its hover altitude increase acceleration upward until that point.
					//if (ship.verticalSpeed < cutoffCondition)
					if (ship.radarAltitude / hoverAlt < 0.25f)
					{
						//this.part.AddForceAtPosition (((-1f) * ship.graviticAcceleration.magnitude - maxVerticalAccel)/numActiveEngines * (float)ship.totalMass * ship.graviticAcceleration.normalized, ship.CoM);

						// Replaced the original method of applying the force through the COM to applying the same acceleration to
						// each part with a rigid body.  This mitigates or eliminates the strange "slumping" of vessels as KSP
						// still keeps track of each part's mass, and does not treat the vessel as a monolithic object with a single mass.
						// This method is used in all of the Takeoff/Hover/Landing code.
						foreach (var part in ship.parts.Where(p => p.Rigidbody != null))
						{
							// Skip parts with a physicalSignificance = NONE
							// This became necessary after discovering that a shuttlepod with ConformalDecals text decals
							// exhibited extreme acceleration in Takeoff mode (almost instantaeous shock effects at launch).
							// It seems that if the part has a rigidbody, but physical significance of "NONE", it applied the acceleration,
							// but as there was "no mass", it was applying multiples of the acceleration to the parent part (the Shuttlepod fuselage).
							if (part.physicalSignificance == Part.PhysicalSignificance.NONE)
							{
								continue;
							}

							// Apply force in "Acceleration" mode, allowing me to just calculate the acceleration
							// and not have to worry about getting part dry mass and the mass of all resources.
							// This code is similarly applied (with appropriate modifications) in all of the Takeoff/Hover/Landing code.
							part.Rigidbody.AddForce(((-1f) * ship.graviticAcceleration.magnitude - maxVerticalAccel) / numActiveEngines * ship.graviticAcceleration.normalized, ForceMode.Acceleration);
						}

						//Debug.Log ("[ScifiShipyardsRedux] Current Thrust Percentage: " + this.thrustPercentage);
					}

					// If the vessel reaches half of its hover altitude simply switch to cancelling out gravity.
					// This should be sufficient time to slow down some to come to a hover, or simply continue upwards under impulse thrust
					if (ship.radarAltitude / hoverAlt >= 0.25f)
					{
						//this.part.AddForceAtPosition (((-1f)*ship.graviticAcceleration.magnitude)/numActiveEngines * (float)ship.totalMass*ship.graviticAcceleration.normalized, ship.CoM);
						foreach (var part in ship.parts.Where(p => p.Rigidbody != null))
						{
							if (part.physicalSignificance == Part.PhysicalSignificance.NONE)
							{
								continue;
							}
							part.Rigidbody.AddForce(((-1f) * ship.graviticAcceleration.magnitude) / numActiveEngines * ship.graviticAcceleration.normalized, ForceMode.Acceleration);
						}

						// Added to cancel out any negative vertical speed by adding maxVerticalAccel
						if (ship.verticalSpeed < 0f)
						{
							foreach (var part in ship.parts.Where(p => p.Rigidbody != null))
							{
								if (part.physicalSignificance != Part.PhysicalSignificance.NONE)
								{
									part.Rigidbody.AddForce(((-1f) * ship.graviticAcceleration.magnitude - maxVerticalAccel) / numActiveEngines * ship.graviticAcceleration.normalized, ForceMode.Acceleration);
								}
							}
						}

					}

				}


				// Hover code - not a separate mode, just some code to get it to hover
				if (this.EngineIgnited && (ship.radarAltitude >= hoverAlt) && (ship.radarAltitude <= 2f * hoverAlt) && (throttle > 0f))
				{
					//this.part.AddForceAtPosition (((-1f)*ship.graviticAcceleration.magnitude)/numActiveEngines *(float)ship.totalMass*ship.graviticAcceleration.normalized, ship.CoM);
					foreach (var part in ship.parts.Where(p => p.Rigidbody != null))
					{
						if (part.physicalSignificance == Part.PhysicalSignificance.NONE)
						{
							continue;
						}
						part.Rigidbody.AddForce(((-1f) * ship.graviticAcceleration.magnitude) / numActiveEngines * ship.graviticAcceleration.normalized, ForceMode.Acceleration);
					}
				}



				// Landing code
				if (this.EngineIgnited && (ship.radarAltitude <= hoverAlt) && (ship.radarAltitude > 0d) && !takeOff)
				{
					//ship.acceleration = ship.graviticAcceleration - ship.graviticAcceleration - (2 * ship.radarAltitude * maxVerticalAccel / hoverAlt) * ship.up;
					//this.part.AddForceAtPosition(((-1)*ship.graviticAcceleration.magnitude - 0.001f + 0.5*maxVerticalAccel*Mathf.Sin(((float)ship.radarAltitude/hoverAlt)*Mathf.PI))*(float)ship.totalMass*ship.graviticAcceleration.normalized,ship.CoM);

					// If our vertical speed is positive, we should decrease any vertical forces to below the ship's weight
					if (ship.verticalSpeed > 0d)
					{
						//this.part.AddForceAtPosition (((-1f) * ship.graviticAcceleration.magnitude + maxVerticalAccel)/numActiveEngines * (float)ship.totalMass * ship.graviticAcceleration.normalized, ship.CoM);
						foreach (var part in ship.parts.Where(p => p.Rigidbody != null))
						{
							if (part.physicalSignificance == Part.PhysicalSignificance.NONE)
							{
								continue;
							}
							part.Rigidbody.AddForce(((-1f) * ship.graviticAcceleration.magnitude + maxVerticalAccel) / numActiveEngines * ship.graviticAcceleration.normalized, ForceMode.Acceleration);
						}
					}

					// If our vertical speed is negative, but above safe landing speed, apply a constant acceleration upwards
					if (ship.verticalSpeed < 0d && ship.verticalSpeed < -5d)
					{

						//this.part.AddForceAtPosition (((-1f) * ship.graviticAcceleration.magnitude - maxVerticalAccel*2f*(-1f*(float)ship.verticalSpeed/5f))/numActiveEngines * ship.totalMass * ship.graviticAcceleration.normalized, ship.CoM);
						foreach (var part in ship.parts.Where(p => p.Rigidbody != null))
						{
							if (part.physicalSignificance == Part.PhysicalSignificance.NONE)
							{
								continue;
							}
							part.Rigidbody.AddForce(((-1f) * ship.graviticAcceleration.magnitude - maxVerticalAccel * 2f * (-1f * (float)ship.verticalSpeed / 5f)) / numActiveEngines * ship.graviticAcceleration.normalized, ForceMode.Acceleration);
						}
					}
				}
			}

			// Below is the code to control the actual thrusting of the impulse engines.  There is no need to account for the number
			// of active engines here as the maxAccel variable will be treated as the max acceleration provided by one engine, just
			// as one would expect of a normal ModuleEngines engine.
			/*if (HighLogic.LoadedSceneIsFlight && this.EngineIgnited && throttle > 0f && DTAmnt > minDilithium)
			{
				// Testing the ctrlState pitch, roll, and yaw attributes to see if they capture the inputs
				//currentPitchInput = FlightGlobals.ActiveVessel.ctrlState.pitch;
				//currentRollInput = FlightGlobals.ActiveVessel.ctrlState.roll;
				//currentYawInput = FlightGlobals.ActiveVessel.ctrlState.yaw;

				// Drain  Liquid Dilithium while the engine is thrusting
				this.vessel.RequestResource (this.part, DTID, minDilithium*(1f-engineEfficiency) * (1 + throttle) * TimeWarp.fixedDeltaTime, true);

				// Actually apply the thrust to the ship center of mass along the forward vector
				this.part.AddForceAtPosition (maxAccel * 9.81f * (this.thrustPercentage/100f) * throttle * (float)ship.totalMass * thrustDirection * ship.vesselTransform.up,ship.CoM);

			}*/

			// Below is the updated code to control the primary thrusting capability of the impulse engines.  This version of the code,
			// the original of which is above commented out, adds "thrust vectoring" capability.  I have placed it in quotes because, in a way,
			// it is not really thrust vectoring, but applying off-axis acceleration to allow the impulse engines to participate in attitude
			// control.  This should emulate thrust vectoring and make the vessels easier to control in-atmosphere.
			if (HighLogic.LoadedSceneIsFlight && this.EngineIgnited && throttle > 0f && DTAmnt > minDilithium)
			{
				// Testing the ctrlState pitch, roll, and yaw attributes to see if they capture the inputs
				//currentPitchInput = FlightGlobals.ActiveVessel.ctrlState.pitch;
				//currentRollInput = FlightGlobals.ActiveVessel.ctrlState.roll;
				//currentYawInput = FlightGlobals.ActiveVessel.ctrlState.yaw;

				// Drain  Liquid Dilithium while the engine is thrusting
				this.vessel.RequestResource(this.part, DTID, minDilithium * (1f - engineEfficiency) * (1 + throttle) * TimeWarp.fixedDeltaTime, true);

				// Set the angles to compute the components of the acceleration.
				// If thrust vectoring is enabled (thrustVectoring = true), add acceleration using components
				// If thrust vectoring is disabled (thrustVectoring = false), use the original behavior
				if (thrustVectoring)
				{
					// Apply acceleration by components
					// Need to combine the roll and yaw angles to get the off-axis component(s)
					// NOTE:  Pitch Up(S-key) is +1, Pitch Down(W-key) is -1, Roll Right(E-key) is +1, Roll Left (Q-key) is -1,
					// Yaw Right(D-key) is +1, and Yaw Left(A-key) is -1

					// Compute the component of forward thrust accounting for pitch, roll, and yaw
					fwdThrust = maxAccel * (float)Math.Cos(Math.PI * (double)(Math.Abs(pitchInput) * maxVectorAngle) / 180) * (float)Math.Cos(Math.PI * (double)(Math.Abs(rollInput) * maxVectorAngle) / 180) * (float)Math.Cos(Math.PI * (double)(Math.Abs(yawInput) * maxVectorAngle) / 180);

					// Compute the component of pitch thrust
					pitchThrust = maxAccel * (float)Math.Sin(Math.PI * (double)(Math.Abs(pitchInput) * maxVectorAngle) / 180);

					// Compute the component of roll thrust
					rollThrust = maxAccel * (float)Math.Sin(Math.PI * (double)(Math.Abs(rollInput) * maxVectorAngle) / 180);

					// Compute the component of yaw thrust
					yawThrust = maxAccel * (float)Math.Sin(Math.PI * (double)(Math.Abs(yawInput) * maxVectorAngle) / 180);

					// Apply the forward thrust
					// 6 Nov. 2021:  Changed ship.CoM to ship.CurrentCoM, this should prevent any future unexpected torques from changine CoM
					// Torques were noted on the Type F Shuttlecraft and the cause was found to be the production of warpPlasma below the CoM.  This production
					// caused the CoM to shift downward enough that the ship.CoM, which seems not to update each frame, was above the new actual CoM.
					// Hopefully using ship.CurrentCoM will prevent this and actually have thrust always applied through the CoM regardless of propellant usages,
					// or shuttlecraft embarked.
					//this.part.AddForceAtPosition(fwdThrust * 9.81f * (this.thrustPercentage/100f) * throttle * (float)ship.totalMass * thrustDirection * ship.vesselTransform.up,ship.CoM);

					foreach (var part in ship.parts.Where(p => p.Rigidbody != null))
					{
						if (part.physicalSignificance == Part.PhysicalSignificance.NONE)
						{
							continue;
						}
						part.Rigidbody.AddForce(fwdThrust * 9.81f * (this.thrustPercentage / 100f) * throttle * thrustDirection * ship.vesselTransform.up, ForceMode.Acceleration);
					}

					// Apply pitch thrust
					// If the pitch is a pitch up, the pitch input will be positive.  In this case, we apply the force (hopefully) in the forward direction (downward with respect
					// to the way the vessel actually flies) at a point along the long axis of the ship near the rear of the ship.
					// If the pitch is a pitch down, we simply flip the direction of the force by multiplying by -1.
					if (pitchInput > 0)
					{
						this.part.AddForceAtPosition(pitchThrust * 9.81f * (this.thrustPercentage / 100f) * throttle * (float)ship.totalMass * ship.vesselTransform.forward, (ship.CoM - (sizeOfVessel.z * ship.vesselTransform.up / 2)));
					}
					else
					{
						this.part.AddForceAtPosition(pitchThrust * 9.81f * (this.thrustPercentage / 100f) * throttle * (float)ship.totalMass * (-1f) * ship.vesselTransform.forward, (ship.CoM - (sizeOfVessel.z * ship.vesselTransform.up / 2)));
					}

					// Apply roll thrust
					// If the roll is to the right, the roll input will be positive.  In this case, we apply the force (hopefully) in the forward direction (downward with respect
					// to the way the vessel actually flies) at a point along the x-axis of the ship near the starboard side of the ship.
					// If the roll is to the left, we simply flip the direction of the force by multiplying by -1.
					if (rollInput > 0)
					{
						this.part.AddForceAtPosition(rollThrust * 9.81f * (this.thrustPercentage / 100f) * throttle * (float)ship.totalMass * ship.vesselTransform.forward, (ship.CoM + (sizeOfVessel.y * ship.vesselTransform.right / 2)));
					}
					else
					{
						this.part.AddForceAtPosition(rollThrust * 9.81f * (this.thrustPercentage / 100f) * throttle * (float)ship.totalMass * (-1f) * ship.vesselTransform.forward, (ship.CoM + (sizeOfVessel.y * ship.vesselTransform.right / 2)));
					}

					// Apply yaw thrust
					// If the yaw is to the right, the yaw input will be positive.  In this case, we apply the force (hopefully) in the port direction (to the left with respect
					// to the way the vessel actually flies) at a point along the long axis of the ship near the rear of the ship.
					// If the yaw is to the right, we simply flip the direction of the force by multiplying by removing the mulitplication by -1.
					if (yawInput > 0)
					{
						this.part.AddForceAtPosition(yawThrust * 9.81f * (this.thrustPercentage / 100f) * throttle * (float)ship.totalMass * (-1f) * ship.vesselTransform.right, (ship.CoM - (sizeOfVessel.z * ship.vesselTransform.up / 2)));
					}
					else
					{
						this.part.AddForceAtPosition(yawThrust * 9.81f * (this.thrustPercentage / 100f) * throttle * (float)ship.totalMass * ship.vesselTransform.right, (ship.CoM - (sizeOfVessel.z * ship.vesselTransform.up / 2)));
					}

				}
				else
				{
					// Actually apply the thrust to the ship center of mass along the forward vector
					//this.part.AddForceAtPosition (maxAccel * 9.81f * (this.thrustPercentage/100f) * throttle * (float)ship.totalMass * thrustDirection * ship.vesselTransform.up,ship.CoM);

					foreach (var part in ship.parts.Where(p => p.Rigidbody != null))
					{
						if (part.physicalSignificance == Part.PhysicalSignificance.NONE)
						{
							continue;
						}
						part.Rigidbody.AddForce(maxAccel * 9.81f * (this.thrustPercentage / 100f) * throttle * thrustDirection * ship.vesselTransform.up, ForceMode.Acceleration);
					}
				}

			}
		}
	}
}


 



