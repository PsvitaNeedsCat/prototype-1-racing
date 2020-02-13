﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(CarController))]
public class Player : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image chargeBar;
    [SerializeField] private Image chargeBarBG;
    [SerializeField] private TextMeshProUGUI currentLapText;
    [SerializeField] private TextMeshProUGUI totalLapsText;

    [Header("Player Settings")]

    [Tooltip("Amount of time it takes to charge from empty to full")]
    [SerializeField] private float chargeTime = 1.0f;

    [Tooltip("Amount of force to apply when the player releases their charge (scaled by charge amount)")]
    [SerializeField] private float releaseImpulseAmount = 1.0f;

    [Tooltip("How long the player's 'wind-back' acceleration will decay over")]
    [SerializeField] private float accelDecayTime = 2.0f;

    [Tooltip("Cooldown for a fullycharged release - scales down based on how much you charged up")]
    [SerializeField] private float chargeCooldown = 2.0f;

    [SerializeField] [Range(0.1f, 10.0f)] private float turningSensitivity = 1.0f;

    [SerializeField] int playerNumber = 1;

    public AnimationCurve chargeUpCurve; // Base charge up curve
    public AnimationCurve bonusChargeCurve; // Curve representing how much bonus power you get from charging longer

    private float chargeAmount = 0.0f; // How full the charge bar is
    private float normalisedTimeCharged = 0.0f; // Amount of time spent charging
    private bool isCharging = false;
    private bool chargingUp = true; // Indicates direction of charging - after being fully charged, the bar will deplete
    private float accelAmount = 0.0f;
    private float steeringInput = 0.0f;

    private float cooldownTimer = 0.0f;

    private InputMaster controls;
    private Rigidbody rigidBody;
    private CarController carController;
    private HornScript hornScript;
    private int lapNum = 1;
    private int lastCheckpointPassed = 0;
    private int numCheckpoints;

    public bool IsRespawning
    {
        get { return carController.IsRespawning; }
    }

    private float ChargeAmount
    {
        get { return chargeAmount; }
        set
        {
            chargeAmount = value;
            chargeBar.fillAmount = chargeAmount;
        }
    }

    private int CurrentLapNumber
    {
        get { return lapNum; }
        set
        {
            lapNum = value;
            currentLapText.text = lapNum.ToString();
        }
    }

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        carController = GetComponent<CarController>();
        hornScript = GetComponent<HornScript>();
        ChargeAmount = 0.0f;
        controls = new InputMaster();

        // Made a switch for future proofing
        switch (playerNumber)
        {
            case 1:
            {
                controls.Player1.Enable();

                controls.Player1.ChargePress.performed += _ => StartCharging();
                controls.Player1.ChargeRelease.performed += _ => StopCharging();

                    controls.Player1.HornPress.performed += _ => PressHorn();
                    controls.Player1.HornRelease.performed += _ => ReleaseHorn();

                    controls.Player1.Turning.performed += ctx => Steer(ctx.ReadValue<float>());

                    // Set up devices (Gamepad and keyboard if gamepad is plugged in, else just a keyboard
                    controls.devices = (Gamepad.all.Count >= 1) ? new[] { Gamepad.all[0], Keyboard.all[0] } : controls.devices = new[] { Keyboard.all[0] };

                break;
            }

            case 2:
            {
                controls.Player2.Enable();

                controls.Player2.ChargePress.performed += _ => StartCharging();
                controls.Player2.ChargeRelease.performed += _ => StopCharging();

                    controls.Player2.HornPress.performed += _ => PressHorn();
                    controls.Player2.HornRelease.performed += _ => ReleaseHorn();

                    controls.Player2.Turning.performed += ctx => Steer(ctx.ReadValue<float>());

                    // Set up devices (Gamepad and keyboard if gamepad is plugged in, else just a keyboard
                    controls.devices = (Gamepad.all.Count >= 2) ? new[] { Gamepad.all[1], Keyboard.all[0] } : controls.devices = new[] { Keyboard.all[0] };

                break;
            }

            default:
                break;
        }
        
    }

    private void Start()
    {
        totalLapsText.text = GameManager.Instance.numLaps.ToString();
        numCheckpoints = GameManager.Instance.numCheckpoints;
    }

    private void Update()
    {
        ChargeBarUpdate();
        AccelerationUpdate();
        ChargingUpdate();
    }

    private void FixedUpdate()
    {
        if (isCharging)
        {
            transform.Rotate(Vector3.up, steeringInput * turningSensitivity);
            carController.Move(0.0f, accelAmount);
        }
        else
        {
            carController.Move(steeringInput, accelAmount);
        }

    }

    private void StartCharging()
    {
        if (cooldownTimer > 0.001f) { return; }
        if (!carController.IsGrounded) { return; }

        carController.StopAllWheels();
        carController.WheelCollidersFriction(false);
        carController.CanSteer = false;
        // rigidBody.velocity = Vector3.zero;
        // rigidBody.isKinematic = true;
        isCharging = true;
        chargingUp = true;
        ChargeAmount = 0.0f;
        normalisedTimeCharged = 0.0f;
    }

    private void StopCharging()
    {
        if (!isCharging) { return; }

        isCharging = false;
        rigidBody.isKinematic = false;
        accelAmount = chargeAmount;
        carController.CanSteer = true;
        carController.WheelCollidersFriction(true);
        cooldownTimer = chargeCooldown * chargeAmount;

        float longChargeBonus = Mathf.Clamp(bonusChargeCurve.Evaluate(normalisedTimeCharged), 0.0f, 999.0f);
        carController.ApplyForwardImpulse(releaseImpulseAmount * (chargeAmount + longChargeBonus));

        ChargeAmount = 0.0f;
    }

    void PressHorn()
    {
        hornScript.PlayHorn();
    }

    void ReleaseHorn()
    {
        hornScript.StopHorn();
    }

    private void ChargingUpdate()
    {
        cooldownTimer = Mathf.Clamp(cooldownTimer - Time.deltaTime, 0.0f, chargeCooldown);

        if (!isCharging) { return; }

        float deltaCharge = Time.deltaTime / chargeTime;

        if (chargingUp)
        {
            normalisedTimeCharged = Mathf.Clamp(normalisedTimeCharged + deltaCharge, 0.0f, 1.0f);
            ChargeAmount = chargeUpCurve.Evaluate(normalisedTimeCharged);

            // If fully charged, start uncharging
            if (normalisedTimeCharged > 0.999f)
            {
                chargingUp = false;
            }
        }
        else
        {
            normalisedTimeCharged = Mathf.Clamp(normalisedTimeCharged - deltaCharge, 0.0f, 1.0f);
            ChargeAmount = chargeUpCurve.Evaluate(normalisedTimeCharged);

            // If empty charge, start charging up again
            if (normalisedTimeCharged < 0.001f)
            {
                chargingUp = true;
            }
        }
    }

    private void ChargeBarUpdate()
    {
        if (cooldownTimer > 0.001f)
        {
            chargeBar.transform.localScale = Vector3.zero;
            chargeBarBG.transform.localScale = Vector3.zero;
        }
        else
        {
            chargeBar.transform.localScale = Vector3.one;
            chargeBarBG.transform.localScale = Vector3.one;
        }
    }

    private void AccelerationUpdate()
    {
        accelAmount = Mathf.Clamp(accelAmount - (Time.deltaTime / accelDecayTime), 0.0f, 1.0f);
    }

    private void Steer(float horInput)
    {
        steeringInput = horInput;
    }

    public float Respawn()
    {
        return carController.RespawnCar();
    }

    public void PassedCheckpoint(int checkpointNum)
    {
        // Check if in order
        if (checkpointNum == lastCheckpointPassed + 1)
        {
            // Check if lap complete
            if (checkpointNum == numCheckpoints)
            {
                if (CurrentLapNumber == GameManager.Instance.numLaps)
                {
                    GameManager.Instance.raceComplete = true;
                    GameManager.Instance.winner = playerNumber;
                    return;
                }

                CurrentLapNumber++;
                lastCheckpointPassed = 0;
            }
            else
            {
                lastCheckpointPassed = checkpointNum;
            }

        }
    }


    public void SetInputControl(bool canInput)
    {
        if (canInput)
        {
            controls.Enable();
        }
        else
        {
            controls.Disable();

            if (isCharging)
            {
                StopCharging();
            }
        }
    }

    public void ApplyImpulse(Vector3 impulse)
    {
        carController.ApplyImpulse(impulse);
    }

    public void ApplyForce(Vector3 force)
    {
        carController.ApplyForce(force);
    }
}
