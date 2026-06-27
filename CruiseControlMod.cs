using System;
using System.Drawing;
using System.Windows.Forms;
using GTA;
using GTA.Native;


public class CruiseControlMod : Script
{
    // Settings & Keys (Defaults)
    private Keys toggleKey = Keys.Y;
    private Keys speedUpKey = Keys.I;
    private Keys speedDownKey = Keys.K;
    
    // Configurable Options
    private float speedStepMph = 2f;
    private bool enableHud = true;

    // Mod States
    private bool isCruiseActive = false;
    private float targetSpeedMps = 0f;
    private bool isBrakeHeld = false;

    // Conversion Factors
    private const float MphToMps = 0.44704f;
    private const float MpsToMph = 2.23694f;

    // UI Elements
    private TextElement hudHeader;
    private TextElement hudStatus;
    private TextElement hudSpeed;

    public CruiseControlMod()
    {
        LoadSettings();
        InitializeHud();

        Tick += OnTick;
        KeyDown += OnKeyDown;
    }

    private void LoadSettings()
    {
        ScriptSettings config = ScriptSettings.Load("scripts\\GtaCruiseControl.ini");

        toggleKey = config.GetValue<Keys>("Keybinds", "ToggleCruiseKey", Keys.Y);
        speedUpKey = config.GetValue<Keys>("Keybinds", "SpeedUpKey", Keys.I);
        speedDownKey = config.GetValue<Keys>("Keybinds", "SpeedDownKey", Keys.K);

        speedStepMph = config.GetValue<float>("Options", "SpeedStepMph", 2f);
        enableHud = config.GetValue<bool>("Options", "EnableHUD", true);

        config.Save();
    }

    private void InitializeHud()
    {
        // Scaled down to standard sizes (0.32f and 0.28f) and adjusted spacing
        hudHeader = new TextElement("Cruise Control", new Point(40, 490), 0.32f, Color.FromArgb(200, 20, 20));
        hudStatus = new TextElement("Enabled", new Point(40, 508), 0.28f, Color.FromArgb(163, 214, 255));
        hudSpeed = new TextElement("Speed: 0 MPH", new Point(40, 523), 0.28f, Color.FromArgb(163, 214, 255));
        
        hudHeader.Font = GTA.UI.Font.ChaletLondon;
        hudStatus.Font = GTA.UI.Font.ChaletLondon;
        hudSpeed.Font = GTA.UI.Font.ChaletLondon;
    }

    private void OnTick(object sender, EventArgs e)
    {
        Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;

        if (playerVehicle == null || !playerVehicle.IsAlive)
        {
            isCruiseActive = false;
            return;
        }

        if (isCruiseActive)
        {
            // 1. Drop out completely if user manually handles raw throttle acceleration
            if (GTA.Input.Controls.IsDisabledControlPressed(ControlType.PlayerControl, (GTA.Input.ControlAction)GTA.Control.VehicleAccelerate))
            {
                isCruiseActive = false;
                return;
            }

            // 2. Native Dynamic Reverse Detection: 
            // If the user is holding down their native brake/reverse control AND the car is stopped or moving backward, disable cruise.
            if (GTA.Input.Controls.IsDisabledControlPressed(ControlType.PlayerControl, (GTA.Input.ControlAction)GTA.Control.VehicleBrake))
            {
                // check if vehicle is essentially stopped or going backward relative to its heading
                if (playerVehicle.Speed < 0.5f || Function.Call<bool>(Hash.IS_VEHICLE_IN_REVERSE_GEAR, playerVehicle))
                {
                    isCruiseActive = false;
                    return;
                }
            }

            // 3. Route execution if user holds down brakes manually while still rolling forward (Original Loop)
            if (GTA.Input.Controls.IsDisabledControlPressed(ControlType.PlayerControl, (GTA.Input.ControlAction)GTA.Control.VehicleBrake))
            {
                isBrakeHeld = true;
                return; 
            }

            // If brake was released, handle sequential recovery acceleration
            if (isBrakeHeld)
            {
                if (playerVehicle.Speed < targetSpeedMps)
                {
                    playerVehicle.ForwardSpeed = Math.Min(playerVehicle.Speed + (4.0f * Game.LastFrameTime), targetSpeedMps);
                }
                else
                {
                    isBrakeHeld = false;
                }
            }
            else
            {
                // Core Execution Fix: Directly update speed values to process user input increments instantly
                playerVehicle.ForwardSpeed = targetSpeedMps;
            }
        }

        // Draw persistent HUD elements onto screen coordinates if activated
        if (enableHud && isCruiseActive)
        {
            int displaySpeed = (int)Math.Round(targetSpeedMps * MpsToMph);
            hudSpeed.Caption = $"Speed: {displaySpeed} MPH";
            
            hudHeader.Draw();
            hudStatus.Draw();
            hudSpeed.Draw();
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;
        if (playerVehicle == null || !playerVehicle.IsAlive) return;

        // 1. Toggle Control State
        if (e.KeyCode == toggleKey)
        {
            isCruiseActive = !isCruiseActive;
            if (isCruiseActive)
            {
                targetSpeedMps = playerVehicle.Speed;
                isBrakeHeld = false;
            }
        }

        // 2. Process Button Acceleration Increments
        if (e.KeyCode == speedUpKey && isCruiseActive)
        {
            targetSpeedMps += (speedStepMph * MphToMps);
            isBrakeHeld = false; // Bypass recovery lag loops instantly
        }

        // 3. Process Button Deceleration Decrements
        if (e.KeyCode == speedDownKey && isCruiseActive)
        {
            targetSpeedMps = Math.Max(0f, targetSpeedMps - (speedStepMph * MphToMps));
            isBrakeHeld = false; // Bypass recovery lag loops instantly
        }
    }
}
