/* extendedEyeGazeDataProvider and EETDATAProvider parts licensed under the MIT license 
 (extendedEyeGazeDataProvider.cs, and parts of this code based on the original EETDataProviderTest.cs).
 Therefore take into account copyright (c) Microsoft Corporation.
 Code is massivaly adapted by G.Luijten to fit for clinical workflow in the HINTS exam.
*/

#region Used NameSpaces: Divided in General, UWP and Unity
using System;
using UnityEngine;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using System.Collections.Generic;

// Platform-specific imports for HoloLens/UWP
#if WINDOWS_UWP
    using Windows.Storage;      // For accessing file system on HoloLens
    using UnityEngine.Windows.Speech;
    using System.Linq;
    using Windows.System;       // For launching external apps like eye tracking calibration
    using Windows.Media.Capture; // For recording patient's voice
    using Windows.Media.MediaProperties;
#endif

// Unity Editor specific imports
#if UNITY_EDITOR
    using System.IO;            // For file operations in the Unity Editor
#endif
#endregion

#region JSON serialization for configuration data
using Newtonsoft.Json; // For writing/reading configuration to JSON files

/// <summary>
/// Stores application configuration as key-value pairs
/// Used to remember calibration state between sessions
/// </summary>
[Serializable]
internal class ConfigData
{
    public Dictionary<string, bool> data;
}
#endregion

/// <summary>
/// Controls eye tracking data collection and clinical workflow for the HINTS exam
/// Manages audio instructions, data recording, and saving of eye tracking measurements
/// This script orchestrates the entire examination process for vestibular testing
/// </summary>
public class EETDataProviderHLhints : MonoBehaviour
{   
    #region Debugging tools
    // Controls debug output and sounds
    public bool debugBool = false;
    public AudioSource debugAudioSound;
    public AudioSource debugConfirmSavedSound;
    #endregion
    
    #region UI Buttons and interaction state
    // Calibration UI and state
    [SerializeField]
    private GameObject calibrationButton;
    private bool calibrationBool = false;
    
    // Start examination UI and state
    [SerializeField]
    private GameObject startButton;
    private bool startBool = false;
    #endregion
   
    #region Configuration persistence
    // Path to JSON config file and loaded configuration
    private string configFilePath;
    private ConfigData configData;
    #endregion

    #region Eye tracking core components
    // The provider that gives access to eye tracking data
    [SerializeField]
    private ExtendedEyeGazeDataProvider extendedEyeGazeDataProvider;
    private DateTime timestamp;
    private ExtendedEyeGazeDataProvider.GazeReading gazeReading;
    #endregion
    
    #region Data collection and storage
    // StringBuilder for efficiency when collecting large amounts of data
    private StringBuilder longMessageForSaving = new StringBuilder();
    private string filename;
    private string timeStampStart;
    private string filenameAddition = "_longmessage";

    // File writing component for Unity Editor
    #if UNITY_EDITOR
        private StreamWriter writer;
    #endif
    #endregion
   
    #region Workflow timing and state management
    // Counter used to initialize startTime only once
    private int j = 0;
    // Indicates if patient voice recording is complete
    public bool savedWavBool = false;

    // Eye tracking detection parameters
    private bool startLogicBool = false;
    private float startTime;
    private float desiredDelay = 0.2f; // Time eyes must be tracked before proceeding
    
    // Timing parameters for each examination phase
    // Initial instructions
    private bool isStartSoundPlayingBool = false;
    private bool startMessageBool = false;
    private float startMeasureTimer = 0f;
    private float startDuration = 2.0f; // Base duration, audio clip length will be added
    
    // "Look around the room" phase
    private bool isRaumSoundPlayingBool = false;
    private bool raumMessageBool = false;
    private float raumMeasureTimer = 0f;
    private float raumDuration = 60.0f; // 60 seconds for room examination
    
    // "Hold still" phase
    private bool isStillSoundPlayingBool = false;
    private bool stillMessageBool = false;
    private float stillMeasureTimer = 0f;
    private float stillDuration = 10.0f; // 10 seconds for still examination
    
    // "Look at nose" phase
    private bool isNaseSoundPlayingBool = false;
    private bool naseMessageBool = false;
    private float naseMeasureTimer = 0f;
    private float naseDuration = 15.0f; // 15 seconds for nose examination
    
    // "Look left" phase
    private bool isLinksSoundPlayingBool = false;
    private bool linksMessageBool = false;
    private float linksMeasureTimer = 0f;
    private float linksDuration = 15.0f; // 15 seconds for left examination
    
    // "Look right" phase
    private bool isRechtsSoundPlayingBool = false;
    private bool rechtsMessageBool = false;
    private float rechtsMeasureTimer = 0f;
    private float rechtsDuration = 15.0f; // 15 seconds for right examination
    
    // "Look up at ceiling" phase
    private bool isDeckeSoundPlayingBool = false;
    private bool deckeMessageBool = false;
    private float deckeMeasureTimer = 0f;
    private float deckeDuration = 15.0f; // 15 seconds for ceiling examination
    
    // "Look down at floor" phase
    private bool isBodenSoundPlayingBool = false;
    private bool bodenMessageBool = false;
    private float bodenMeasureTimer = 0f;
    private float bodenDuration = 15.0f; // 15 seconds for floor examination
    
    // Notification sound timing
    private bool blingAudioSoundBool = false;
    private float delayBlingMeasureTimer = 0f;
    private float delayBlingDuration = 1.0f;
    
    // Auto-save timing
    private float delaySaveInt = 120.0f; // 2 minutes before auto-saving data
    private bool isSaveScheduledBool = false;
    #endregion

    #region State machine for examination workflow
    /// <summary>
    /// Represents the current state in the examination workflow
    /// The state machine controls the progression through different examination phases
    /// </summary>
    private enum State
    {
        StartLogic,      // Waiting for eye tracking detection
        StartSound,      // Playing initial instructions
        RaumSound,       // Room scan examination
        StillSound,      // Hold still examination
        NaseSound,       // Look at nose examination
        LinksSound,      // Look left examination
        RechtsSound,     // Look right examination
        DeckeSound,      // Look up at ceiling examination
        BodenSound,      // Look down at floor examination
        HeadMovement,    // Head movement examination
        BlingSound       // Final notification and data collection
    }
    private State CurrentState;
    #endregion
    
    #region Eye and head tracking measurement data
    // World space coordinates (absolute position in 3D space)
    private UnityEngine.Vector3 leftEyeWorldPosition;
    private UnityEngine.Vector3 leftEyeWorldDirection;
    private UnityEngine.Vector3 rightEyeWorldPosition;
    private UnityEngine.Vector3 rightEyeWorldDirection;
    private UnityEngine.Vector3 combinedEyeWorldPosition;
    private UnityEngine.Vector3 combinedEyeWorldDirection;
    
    // Camera space coordinates (relative to the HoloLens camera)
    private UnityEngine.Vector3 leftEyeCameraPosition;
    private UnityEngine.Vector3 leftEyeCameraDirection;
    private UnityEngine.Vector3 rightEyeCameraPosition;
    private UnityEngine.Vector3 rightEyeCameraDirection;
    private UnityEngine.Vector3 combinedEyeCameraPosition;
    private UnityEngine.Vector3 combinedEyeCameraDirection;
    
    // Head tracking data (position and orientation of the HoloLens)
    private UnityEngine.Vector3 headPosition;
    private UnityEngine.Vector3 headEulerAngles;
    private UnityEngine.Quaternion headQuaternion;
    #endregion 
    
    #region Audio instruction files
    // Audio clips for various examination phases
    public AudioSource sageIhreNameSound;   // "Say your name" prompt
    public AudioSource startAudioSound;     // Initial instructions
    public AudioSource raumAudioSound;      // "Look around the room" instructions
    public AudioSource stillAudioSound;     // "Hold still" instructions
    public AudioSource naseAudioSound;      // "Look at nose" instructions
    public AudioSource linksAudioSound;     // "Look left" instructions
    public AudioSource rechtsAudioSound;    // "Look right" instructions
    public AudioSource deckeAudioSound;     // "Look up at ceiling" instructions
    public AudioSource bodenAudioSound;     // "Look down at floor" instructions
    public AudioSource blingAudioSound;     // Notification sound
    #endregion

    /// <summary>
    /// Initializes the application, reads or creates configuration,
    /// sets up timers, and prepares for data collection
    /// </summary>
    private async void Start()
    {
        // Create or read the configuration file
        await InitializeConfigFile();

        // Set timers for each phase by adding the audio clip duration
        // This ensures each phase lasts at least as long as its instruction audio
        startDuration += startAudioSound.clip.length; 
        raumDuration += raumAudioSound.clip.length; 
        stillDuration += stillAudioSound.clip.length;
        naseDuration += naseAudioSound.clip.length; 
        linksDuration += linksAudioSound.clip.length; 
        rechtsDuration += rechtsAudioSound.clip.length;
        deckeDuration += deckeAudioSound.clip.length; 
        bodenDuration += bodenAudioSound.clip.length;
        delayBlingDuration += blingAudioSound.clip.length;

        // Attempt to set framerate to match eye tracker capabilities
        // Note: May not be effective on HoloLens 2
        Application.targetFrameRate = 90;
        
        // Initialize the examination state machine
        CurrentState = State.StartLogic;

        // Set timestamp for file naming and create CSV header for data
        timeStampStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        timeStampStart = timeStampStart.Replace("-", "_").Replace(" ", "_").Replace(":", "_");
        
        // Create header row for the CSV data file with all measurement columns
        longMessageForSaving.Append("timestamp, " +     
            "worldLeftEyePosition, worldLeftEyeDirection, worldRightEyePosition, worldRightEyeDirection, worldCombinedEyePosition, worldCombinedEyeDirection, " +
            "cameraLeftEyePosition, cameraLeftEyeDirection, cameraRightEyePosition, cameraRightEyeDirection, cameraCombinedEyePosition, cameraCombinedEyeDirection, " +
            "headPosition, headEulerAngles, headQuaternion " +
            "\n"
        );

        // Log initialization details
        Debug.Log($"Initialization done start program at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}\n" +
                $"desiredDelay i.e. eye detection duraction before start is {desiredDelay} seconds\n" +
                $"Duration of start clip is {startAudioSound.clip.length} seconds\n" +
                $"Duration before auto saving is {delaySaveInt} seconds"
        );
    }

    /// <summary>
    /// Main update loop that manages the state machine for the examination workflow
    /// and collects eye tracking data in each state
    /// </summary>
    void Update()
    {
        // Only proceed if voice recording is complete
        if(savedWavBool)
        {
            // State machine for the examination workflow
            switch (CurrentState)
            {
                // PHASE 1: Wait for eye detection before proceeding
                case State.StartLogic:
                    // Initialize start time once
                    if(j == 0)
                    {
                        startTime = Time.time;
                        j += 1;
                    }
                    
                    // Check if eyes are detected consistently
                    startLogic();

                    // Proceed to next phase when eye tracking is stable
                    if(startLogicBool)
                    {
                        CurrentState = State.StartSound;
                    }
                    break;

                // PHASE 2: Play initial instructions and begin measurements
                case State.StartSound:
                    // Collect eye tracking data
                    measurements();
                    
                    // Play the start instructions audio once
                    if(!isStartSoundPlayingBool)
                    {
                        StartCoroutine(playInstructionSound(() => isStartSoundPlayingBool = true, startAudioSound));
                    }
                    // Continue measurements while timer runs
                    else if(isStartSoundPlayingBool && startMeasureTimer < startDuration)
                    {
                        // Add marker in data file for this phase (once)
                        if (!startMessageBool)
                        {
                            Debug.Log($"Start measurements at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}, started application at {timeStampStart}");
                            longMessageForSaving.Append($"\nStart recording: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}, , started application at {timeStampStart}\n");
                            startMessageBool = true;
                        }

                        // Increment timer
                        startMeasureTimer += Time.deltaTime;
                    }
                    
                    // Proceed to next phase when timer expires
                    if(startMeasureTimer >= startDuration)
                    {
                        CurrentState = State.RaumSound;
                    }
                    break;
                
                // PHASE 3: "Look around the room" examination
                case State.RaumSound:
                    // Collect eye tracking data
                    measurements();
                    
                    // Play the room examination instructions once
                    if(!isRaumSoundPlayingBool)
                    {
                        StartCoroutine(playInstructionSound(() => isRaumSoundPlayingBool = true, raumAudioSound));
                    }
                    // Continue measurements while timer runs
                    else if(isRaumSoundPlayingBool && raumMeasureTimer < raumDuration)
                    {
                        // Add marker in data file for this phase (once)
                        if (!raumMessageBool)
                        {
                            Debug.Log($"Raum measurements instruction at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}");
                            longMessageForSaving.Append($"\nStart raum instructions at: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}\n");
                            raumMessageBool = true;
                        }

                        // Increment timer
                        raumMeasureTimer += Time.deltaTime;
                    }
                    
                    // Proceed to next phase when timer expires
                    if(raumMeasureTimer >= raumDuration)
                    {
                        CurrentState = State.StillSound;
                    }
                    break;

                // PHASE 4: "Hold still" examination
                case State.StillSound:
                    // Collect eye tracking data
                    measurements();
                    
                    // Play the hold still instructions once
                    if(!isStillSoundPlayingBool)
                    {
                        StartCoroutine(playInstructionSound(() => isStillSoundPlayingBool = true, stillAudioSound));
                    }
                    // Continue measurements while timer runs
                    else if(isStillSoundPlayingBool && stillMeasureTimer < stillDuration)
                    {
                        // Add marker in data file for this phase (once)
                        if (!stillMessageBool)
                        {
                            Debug.Log($"Still measurements instruction at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}");
                            longMessageForSaving.Append($"\nStart still instructions at: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}\n");
                            stillMessageBool = true;
                        }

                        // Increment timer
                        stillMeasureTimer += Time.deltaTime;
                    }
                    
                    // Proceed to next phase when timer expires
                    if(stillMeasureTimer >= stillDuration)
                    {
                        CurrentState = State.NaseSound;
                    }
                    break;

                // PHASE 5: "Look at nose" examination
                case State.NaseSound:
                    // Collect eye tracking data
                    measurements();
                    
                    // Play the nose focus instructions once
                    if(!isNaseSoundPlayingBool)
                    {
                        StartCoroutine(playInstructionSound(() => isNaseSoundPlayingBool = true, naseAudioSound));
                    }
                    // Continue measurements while timer runs
                    else if(isNaseSoundPlayingBool && naseMeasureTimer < naseDuration)
                    {
                        // Add marker in data file for this phase (once)
                        if (!naseMessageBool)
                        {
                            Debug.Log($"Nase measurements instruction at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}");
                            longMessageForSaving.Append($"\nStart nase instructions at: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}\n");
                            naseMessageBool = true;
                        }

                        // Increment timer
                        naseMeasureTimer += Time.deltaTime;
                    }
                    
                    // Proceed to next phase when timer expires
                    if(naseMeasureTimer >= naseDuration)
                    {
                        CurrentState = State.LinksSound;
                    }
                    break;

                // PHASE 6: "Look left" examination
                case State.LinksSound:
                    // Collect eye tracking data
                    measurements();
                    
                    // Play the look left instructions once
                    if(!isLinksSoundPlayingBool)
                    {
                        StartCoroutine(playInstructionSound(() => isLinksSoundPlayingBool = true, linksAudioSound));
                    }
                    // Continue measurements while timer runs
                    else if(isLinksSoundPlayingBool && linksMeasureTimer < linksDuration)
                    {
                        // Add marker in data file for this phase (once)
                        if (!linksMessageBool)
                        {
                            Debug.Log($"Links measurements instruction at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}");
                            longMessageForSaving.Append($"\nStart links instructions at: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}\n");
                            linksMessageBool = true;
                        }

                        // Increment timer
                        linksMeasureTimer += Time.deltaTime;
                    }
                    
                    // Proceed to next phase when timer expires
                    if(linksMeasureTimer >= linksDuration)
                    {
                        CurrentState = State.RechtsSound;
                    }
                    break;

                // PHASE 7: "Look right" examination
                case State.RechtsSound:
                    // Collect eye tracking data
                    measurements();
                    
                    // Play the look right instructions once
                    if(!isRechtsSoundPlayingBool)
                    {
                        StartCoroutine(playInstructionSound(() => isRechtsSoundPlayingBool = true, rechtsAudioSound));
                    }
                    // Continue measurements while timer runs
                    else if(isRechtsSoundPlayingBool && rechtsMeasureTimer < rechtsDuration)
                    {
                        // Add marker in data file for this phase (once)
                        if (!rechtsMessageBool)
                        {
                            Debug.Log($"Rechts measurements instruction at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}");
                            longMessageForSaving.Append($"\nStart rechts instructions at: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}\n");
                            rechtsMessageBool = true;
                        }

                        // Increment timer
                        rechtsMeasureTimer += Time.deltaTime;
                    }
                    
                    // Proceed to next phase when timer expires
                    if(rechtsMeasureTimer >= rechtsDuration)
                    {
                        CurrentState = State.DeckeSound;
                    }
                    break;

                // PHASE 8: "Look up at ceiling" examination
                case State.DeckeSound:
                    // Collect eye tracking data
                    measurements();
                    
                    // Play the look up instructions (with a "hold still" instruction first)
                    if(!isDeckeSoundPlayingBool)
                    {
                        // Chain two audio clips - first still, then ceiling instructions
                        StartCoroutine(playInstructionSound(() => 
                        {
                            // Start the second sound when the first one finishes
                            StartCoroutine(playInstructionSound(() => isDeckeSoundPlayingBool = true, deckeAudioSound));
                        }, stillAudioSound));
                    }
                    // Continue measurements while timer runs
                    else if(isDeckeSoundPlayingBool && deckeMeasureTimer < deckeDuration)
                    {
                        // Add marker in data file for this phase (once)
                        if (!deckeMessageBool)
                        {
                            Debug.Log($"Decke measurements instruction at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}");
                            longMessageForSaving.Append($"\nStart decke instructions at: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}\n");
                            deckeMessageBool = true;
                        }

                        // Increment timer
                        deckeMeasureTimer += Time.deltaTime;
                    }

                    // Proceed to next phase when timer expires
                    if(deckeMeasureTimer >= deckeDuration)
                    {
                        CurrentState = State.BodenSound;
                    }
                    break;

                // PHASE 9: "Look down at floor" examination
                case State.BodenSound:
                    // Collect eye tracking data
                    measurements();

                    // Play the look down instructions (with a "hold still" instruction first)
                    if(!isBodenSoundPlayingBool)
                    {
                        // Chain two audio clips - first still, then floor instructions
                        StartCoroutine(playInstructionSound(() => 
                        {
                            // Start the second sound when the first one finishes
                            StartCoroutine(playInstructionSound(() => isBodenSoundPlayingBool = true, bodenAudioSound));
                        }, stillAudioSound));
                    }
                    // Continue measurements while timer runs
                    else if(isBodenSoundPlayingBool && bodenMeasureTimer < bodenDuration)
                    {
                        // Add marker in data file for this phase (once)
                        if (!bodenMessageBool)
                        {
                            Debug.Log($"Boden measurements instruction at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}");
                            longMessageForSaving.Append($"\nStart boden instructions at: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}\n");
                            bodenMessageBool = true;
                        }

                        // Increment timer
                        bodenMeasureTimer += Time.deltaTime;
                    }
                    
                    // Proceed to next phase when timer expires
                    if(bodenMeasureTimer >= bodenDuration)
                    {
                        // Reset nase sound playing flag for next state
                        isNaseSoundPlayingBool = false;
                        CurrentState = State.HeadMovement;
                    }
                    break;

                // PHASE 10: Head movement examination
                case State.HeadMovement:
                    // Collect eye tracking data
                    measurements();

                    // Play the look at nose instructions again
                    if(!isNaseSoundPlayingBool)
                    {
                        StartCoroutine(playInstructionSound(() => isNaseSoundPlayingBool = true, naseAudioSound));
                    }
                    // Add a short delay before playing notification sound
                    else if(delayBlingMeasureTimer < (delayBlingDuration + 1.0f))
                    {
                        delayBlingMeasureTimer += Time.deltaTime;
                        blingAudioSoundBool = false;
                    }
                    // Play notification sound after delay
                    else if(delayBlingMeasureTimer >= (delayBlingDuration + 1.0f))
                    {
                        if (!blingAudioSoundBool)
                        {
                            Debug.Log($"Head measurements instruction at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}");
                            longMessageForSaving.Append($"\nStart blingAudioSoundBool instructions at: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}\n");
                            StartCoroutine(playInstructionSoundWithoutWait(() => blingAudioSoundBool = true, blingAudioSound));
                        }
                    }

                    // Proceed to final phase when both sounds complete
                    if(isNaseSoundPlayingBool && blingAudioSoundBool)
                    {
                        CurrentState = State.BlingSound;
                    }
                    break;

                // PHASE 11: Final data collection and auto-save scheduling
                case State.BlingSound:
                    // Continue collecting data
                    measurements();
                    
                    // Schedule auto-save if not already scheduled
                    if (!isSaveScheduledBool)
                    {
                        Invoke("DelayedSave", delaySaveInt);
                        isSaveScheduledBool = true;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Outputs debug information and plays a debug sound
    /// </summary>
    private void debugMessage()
    {
        if(debugBool)
        {
            Debug.Log("Programm Started (play music) ... ");
            Debug.Log("Longmessage: " + longMessageForSaving.ToString());
            Debug.Log(longMessageForSaving.ToString());
            StartCoroutine(DebugAudio());  
        }
    }

    /// <summary>
    /// Plays debug audio sound with delay
    /// </summary>
    private IEnumerator DebugAudio()
    {
        debugAudioSound.Play();
        yield return new WaitForSeconds(5.0f);
    }

    /// <summary>
    /// Plays confirmation sound when saving completes
    /// </summary>
    private IEnumerator DebugConfirmSaved()
    {
        debugConfirmSavedSound.Play();
        yield return new WaitForSeconds(5.0f);
    }

    /// <summary>
    /// Handles button click to start the examination after calibration
    /// Triggered by the UI start button
    /// </summary>
    public void startApplicationButton()
    {
        // Only proceed if calibration is complete
        if(calibrationBool)
        {
            #if UNITY_EDITOR
                // Update calibration status in configuration
                if (configData.data.ContainsKey("calibration"))
                {
                    configData.data["calibration"] = false;
                }
                else
                {
                    configData.data.Add("calibration", false);
                }
                // Save updated configuration
                string json = JsonConvert.SerializeObject(configData);
                File.WriteAllText(configFilePath, json);
            #endif

            #if WINDOWS_UWP
                // Update calibration status in configuration
                if (configData.data.ContainsKey("calibration"))
                {
                    configData.data["calibration"] = false;
                }
                else
                {
                    configData.data.Add("calibration", false);
                }
                // Save updated configuration
                string json = JsonConvert.SerializeObject(configData);
                StorageFile configFile = StorageFile.GetFileFromPathAsync(configFilePath).GetAwaiter().GetResult();
                FileIO.WriteTextAsync(configFile, json).GetAwaiter().GetResult();
            #endif

            // Toggle start state
            startBool = !startBool;

            // Log start time
            Debug.Log($"Start recording name at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}, started application at {timeStampStart}");

            // Add marker to data file
            longMessageForSaving.Append($"\nStart recording name: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}, callibration is performed, started application at {timeStampStart}\n");

            // Hide start button
            startButton.SetActive(!startButton.activeSelf);

            // Begin recording patient's voice
            RunRecordAndSaveAudio();
        }

        // Log application state
        Debug.Log($"startBool is {startBool}, calibrationBool is {calibrationBool}");
        if(calibrationBool)
        {
            Debug.Log($"startBool is {startBool}, calibrationBool is {calibrationBool}, application started");
        }
    }

    /// <summary>
    /// Waits for stable eye tracking data before starting the examination
    /// Used to ensure eye tracking is working properly
    /// </summary>
    private void startLogic()
    {   
        // Get the current eye tracking data
        timestamp = DateTime.Now;
        gazeReading = extendedEyeGazeDataProvider.GetCameraSpaceGazeReading(ExtendedEyeGazeDataProvider.GazeType.Combined, timestamp);
        
        #if UNITY_EDITOR
            // For Unity Editor testing, use inverted logic (proceed if NOT valid)
            // This allows testing without actual eye tracking hardware
            if(!gazeReading.IsValid) 
            {
                if (Time.time - startTime > desiredDelay)
                {
                    startLogicBool = true;
                }
            }
            else
            {
                startTime = Time.time;
            }
            return;
        #endif

        #if WINDOWS_UWP
            // For HoloLens, wait for continuous valid eye tracking
            // Only proceed if eyes are tracked consistently for the desired delay
            if(gazeReading.IsValid) 
            {
                if (Time.time - startTime > desiredDelay)
                {
                    startLogicBool = true;
                }
            }
            else 
            {
                // Reset timer if eye tracking is lost
                startTime = Time.time;
            }
        #endif
    }

    /// <summary>
    /// Plays an audio instruction and waits for completion before invoking a callback
    /// Used for sequential audio instructions
    /// </summary>
    /// <param name="setPlaybool">Callback to invoke after audio completes</param>
    /// <param name="audiosource">The audio source to play</param>
    private IEnumerator playInstructionSound(Action setPlaybool, AudioSource audiosource)
    {
        // Play the audio
        audiosource.Play();
        
        // Wait for audio to complete
        yield return new WaitForSeconds(audiosource.clip.length);
        
        // Invoke callback when done
        setPlaybool();
    }

    /// <summary>
    /// Plays an audio instruction immediately and invokes callback without waiting
    /// Used for notification sounds that don't need to block execution
    /// </summary>
    /// <param name="setPlaybool">Callback to invoke immediately</param>
    /// <param name="audiosource">The audio source to play</param>
    private IEnumerator playInstructionSoundWithoutWait(Action setPlaybool, AudioSource audiosource)
    {
        audiosource.Play();
        setPlaybool();
        yield break;
    }

    /// <summary>
    /// Collects eye tracking and head position measurements
    /// This is the core data collection function called during each examination phase
    /// </summary>
    public void measurements()
    {
        // Record current timestamp for this measurement
        timestamp = DateTime.Now;
        longMessageForSaving.Append($"{timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")}, ");

        // WORLD SPACE MEASUREMENTS (absolute 3D positions)
        // Left eye world space data
        gazeReading = extendedEyeGazeDataProvider.GetWorldSpaceGazeReading(ExtendedEyeGazeDataProvider.GazeType.Left, timestamp);
        if (gazeReading.IsValid)
        {
            // Record position and direction of left eye
            leftEyeWorldPosition = gazeReading.EyePosition; 
            leftEyeWorldDirection = gazeReading.GazeDirection;
            longMessageForSaving.Append($"worldLeftEyePosition: {leftEyeWorldPosition:F6}, worldLeftEyeDirection: {leftEyeWorldDirection:F6}, ");
        }
        else
        {
            // Record that data was not available
            longMessageForSaving.Append($"LeftEyeWorldPosition: (NotAvailable), LeftEyeWorldDirection: (NotAvailable), ");
        }
        
        // Right eye world space data
        gazeReading = extendedEyeGazeDataProvider.GetWorldSpaceGazeReading(ExtendedEyeGazeDataProvider.GazeType.Right, timestamp);
        if (gazeReading.IsValid)
        {
            // Record position and direction of right eye
            rightEyeWorldPosition = gazeReading.EyePosition; 
            rightEyeWorldDirection = gazeReading.GazeDirection;
            longMessageForSaving.Append($"worldRightEyePosition: {rightEyeWorldPosition:F6}, worldRightEyeDirection: {rightEyeWorldDirection:F6}, ");
        }
        else
        {
            // Record that data was not available
            longMessageForSaving.Append($"RightEyeWorldPosition: (NotAvailable), RightEyeWorldDirection: (NotAvailable), ");
        }
        
        // Combined eyes world space data
        gazeReading = extendedEyeGazeDataProvider.GetWorldSpaceGazeReading(ExtendedEyeGazeDataProvider.GazeType.Combined, timestamp);
        if (gazeReading.IsValid)
        {
            // Record position and direction of combined gaze
            combinedEyeWorldPosition = gazeReading.EyePosition; 
            combinedEyeWorldDirection = gazeReading.GazeDirection;
            longMessageForSaving.Append($"worldCombinedEyePosition: {combinedEyeWorldPosition:F6}, worldCombinedEyeDirection: {combinedEyeWorldDirection:F6}, ");
        }
        else
        {
            // Record that data was not available
            longMessageForSaving.Append($"CombinedEyeWorldPosition: (NotAvailable), CombinedEyeWorldDirection: (NotAvailable), ");
        }

        // CAMERA SPACE MEASUREMENTS (relative to HoloLens camera)
        // Left eye camera space data
        gazeReading = extendedEyeGazeDataProvider.GetCameraSpaceGazeReading(ExtendedEyeGazeDataProvider.GazeType.Left, timestamp);
        if (gazeReading.IsValid)
        {
            // Record position and direction of left eye relative to camera
            leftEyeCameraPosition = gazeReading.EyePosition; 
            leftEyeCameraDirection = gazeReading.GazeDirection;
            longMessageForSaving.Append($"cameraLeftEyePosition: {leftEyeCameraPosition:F6}, cameraLeftEyeDirection: {leftEyeCameraDirection:F6}, ");
        }
        else
        {
            // Record that data was not available
            longMessageForSaving.Append($"LeftEyeCameraPosition: (NotAvailable), LeftEyeCameraDirection: (NotAvailable), ");
        }
        
        // Right eye camera space data
        gazeReading = extendedEyeGazeDataProvider.GetCameraSpaceGazeReading(ExtendedEyeGazeDataProvider.GazeType.Right, timestamp);
        if (gazeReading.IsValid)
        {
            // Record position and direction of right eye relative to camera
            rightEyeCameraPosition = gazeReading.EyePosition; 
            rightEyeCameraDirection = gazeReading.GazeDirection;
            longMessageForSaving.Append($"cameraRightEyePosition: {rightEyeCameraPosition:F6}, cameraRightEyeDirection: {rightEyeCameraDirection:F6}, ");
        }
        else
        {
            // Record that data was not available
            longMessageForSaving.Append($"RightEyeCameraPosition: (NotAvailable), RightEyeCameraDirection: (NotAvailable), ");
        }
        
        // Combined eyes camera space data
        gazeReading = extendedEyeGazeDataProvider.GetCameraSpaceGazeReading(ExtendedEyeGazeDataProvider.GazeType.Combined, timestamp);
        if (gazeReading.IsValid)
        {
            // Record position and direction of combined gaze relative to camera
            combinedEyeCameraPosition = gazeReading.EyePosition;
            combinedEyeCameraDirection = gazeReading.GazeDirection;
            longMessageForSaving.Append($"cameraCombinedEyePosition: {combinedEyeCameraPosition:F6}, cameraCombinedEyeDirection: {combinedEyeCameraDirection:F6}, ");
        }
        else
        {
            // Record that data was not available
            longMessageForSaving.Append($"CombinedEyeCameraPosition: (NotAvailable), CombinedEyeCameraDirection: (NotAvailable), ");
        }

        // Head tracking and appending to string
        headPosition = Camera.main.transform.position; headEulerAngles = Camera.main.transform.rotation.eulerAngles; headQuaternion = Camera.main.transform.rotation;
        longMessageForSaving.Append($"HeadPosition: {headPosition:F6}, HeadEulerAngles: {headEulerAngles:F6}, HeadQuaternion: {headQuaternion:F6}\n");
    }

    // Save Logic Unity and UWP
    public async void DelayedSave()
    {
        #if UNITY_EDITOR
            saveMessageInUnity();
        #endif
        #if WINDOWS_UWP
        saveMessage(); // no await, so with risk of blocking execution of rest of the code until done. Which is fine in this case, i.e., at the end of the program
        #endif
    }
    private void saveMessageInUnity()
    {   
        #if UNITY_EDITOR
            // Confirm save in debug and longMessagefor saving
            Debug.Log($"Save is executed at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}"); // debug output
            longMessageForSaving.Append($"\nSave is executed at: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}\n");

            // create file name
            filename = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            filename = filename.Replace("-", "_").Replace(" ", "_").Replace(":", "_");
            filename = filename + filenameAddition;
            
            // Define the folder path for HINTS within the Downloads folder on Windows and create if it doesnt exist
            string downloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
            string hintsFolderPath = Path.Combine(downloadFolder, "HINTS");
            string patientFolderPath = Path.Combine(hintsFolderPath, $"Patient_startAppAt_{timeStampStart}");

            // Create the folders if they dont exist
            if (!Directory.Exists(hintsFolderPath))
            {
                Directory.CreateDirectory(hintsFolderPath);
            }
            if (!Directory.Exists(patientFolderPath))
            {
                Directory.CreateDirectory(patientFolderPath);
            }

            // Save file to HINTS folder
            string saveLoc = Path.Combine(patientFolderPath, (filename + filenameAddition + ".txt"));
            StreamWriter writer = new StreamWriter(saveLoc, true);
            writer.WriteLine(longMessageForSaving.ToString());
            writer.Close();

            // debug output
            Debug.Log("saving in " + patientFolderPath + " as " + filename);
            Debug.Log("Saved Longmessage: " + longMessageForSaving.ToString());
            blingAudioSound.Play();
        #endif
    }
    private async Task saveMessage()
    {
        #if WINDOWS_UWP
            // Confirm save in debug and longMessagefor saving
            Debug.Log($"Save is executed at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}"); // debug output
            longMessageForSaving.Append($"\nSave is executed at: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}\n");

            // create file name
            filename = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            filename = filename.Replace("-", "_").Replace(" ", "_").Replace(":", "_");
            filename = filename + filenameAddition;

            // Define the folder path for HINTS within the Music folder on UWP and create if it doesnt exist
            var musicLibrary = KnownFolders.MusicLibrary;
            var hintsFolder = await musicLibrary.CreateFolderAsync("HINTS", CreationCollisionOption.OpenIfExists);
            var subFolder = await hintsFolder.CreateFolderAsync($"StartAppAt{timeStampStart}", CreationCollisionOption.OpenIfExists);

            // Save file to HINTS folder
            var file = await subFolder.CreateFileAsync((filename + ".txt"), CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, (longMessageForSaving.ToString()));

            StartCoroutine(playInstructionSoundWithoutWait(() => {}, blingAudioSound));
        #endif
    }

    // To launch the eye tracking calibration, outside the app
    // Lauche eye tracking
    public void LaunchEyeTracking()
    {
        // Turn of callibration button and set bool to true so that start can be activated
        calibrationBool = !calibrationBool;

        #if UNITY_EDITOR
            // Update the calibration value in the configData dictionary
            if (configData.data.ContainsKey("calibration"))
            {
                configData.data["calibration"] = calibrationBool;
            }
            else
            {
                configData.data.Add("calibration", calibrationBool);
            }
            // Write the updated dictionary back to the JSON file
            string json = JsonConvert.SerializeObject(configData);
            File.WriteAllText(configFilePath, json);
        #endif

        #if WINDOWS_UWP
            // Update the calibration value in the configData dictionary
            if (configData.data.ContainsKey("calibration"))
            {
                configData.data["calibration"] = calibrationBool;
            }
            else
            {
                configData.data.Add("calibration", calibrationBool);
            }
            // Write the updated dictionary back to the JSON file
            string json = JsonConvert.SerializeObject(configData);
            StorageFile configFile = StorageFile.GetFileFromPathAsync(configFilePath).GetAwaiter().GetResult();
            FileIO.WriteTextAsync(configFile, json).GetAwaiter().GetResult();
        #endif

        Debug.Log($"LaunchEyeTracking activated, calibrationBool is {calibrationBool} and written to Config.json");
        calibrationButton.SetActive(!calibrationButton.activeSelf);

        // launch eye calibration within the hololens.
        #if WINDOWS_UWP
            UnityEngine.WSA.Application.InvokeOnUIThread(async () =>
            {
                bool result = await global::Windows.System.Launcher.LaunchUriAsync(new System.Uri("ms-hololenssetup://EyeTracking"));
                if (!result)
                {
                    Debug.LogError("Launching URI failed to launch.");
                }
            }, false);
        #else
            Debug.LogError("Launching eye tracking not supported on non-UWP platforms");
        #endif
    }

    //for recording audio files
    public async Task RecordAndSaveAudio()
    {
        // Give instruction sound
        StartCoroutine(playInstructionSoundWithoutWait(() => { /* Empty callback */ }, sageIhreNameSound));

        #if WINDOWS_UWP
            // Create MediaCapture and its settings
            MediaCapture mediaCapture = new MediaCapture();
            MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Audio
            };

            // Initialize MediaCapture
            await mediaCapture.InitializeAsync(settings);

            // Create file name
            string filename = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            filename = filename.Replace("-", "_").Replace(" ", "_").Replace(":", "_");

            // Define the folder path for HINTS within the Music folder on UWP and create if it doesn't exist
            var musicLibrary = KnownFolders.MusicLibrary;
            var hintsFolder = await musicLibrary.CreateFolderAsync("HINTS", CreationCollisionOption.OpenIfExists);
            var subFolder = await hintsFolder.CreateFolderAsync($"StartAppAt{timeStampStart}", CreationCollisionOption.OpenIfExists);

            // Create the .wav file
            StorageFile storageFile = await subFolder.CreateFileAsync(filename + ".wav", CreationCollisionOption.GenerateUniqueName);

            // Start recording
            MediaEncodingProfile encodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
            await mediaCapture.StartRecordToStorageFileAsync(encodingProfile, storageFile);

            // Wait for 5 seconds + the length of the audio file
            await Task.Delay(8500);

            // Stop recording
            await mediaCapture.StopRecordAsync();

            //end sound
            StartCoroutine(playInstructionSoundWithoutWait(() => {}, blingAudioSound));

            // Set the savedWavBool to true
            savedWavBool = true;
        #endif
        
        #if UNITY_EDITOR
            savedWavBool = true;
        #endif
    }
    public void RunRecordAndSaveAudio()
    {
        _ = RecordAndSaveAudio();
    }
    
    // initialize jason config and reset it
    private async Task InitializeConfigFile()
    {
        #if UNITY_EDITOR
        // define hints folder path for unity editor
        string downloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
        string hintsFolderPath = Path.Combine(downloadFolder, "HINTS");
        // create folder(s) if it doesnt already exists
        if (!Directory.Exists(downloadFolder)) Directory.CreateDirectory(downloadFolder);
        if (!Directory.Exists(hintsFolderPath)) Directory.CreateDirectory(hintsFolderPath);
        // create json file if it doesn't already exists, else read the values from it and activate buttons
        configFilePath = Path.Combine(hintsFolderPath, "Config.json");
        if (!File.Exists(configFilePath))
        {
            configData = new ConfigData();
            configData.data = new Dictionary<string, bool>();
            configData.data["calibration"] = false;

            string json = JsonConvert.SerializeObject(configData);
            File.WriteAllText(configFilePath, json);
            calibrationBool = false;
        }
        else
        {
            string json = File.ReadAllText(configFilePath);
            configData = JsonConvert.DeserializeObject<ConfigData>(json);
            if (configData.data.ContainsKey("calibration") && configData.data["calibration"])
            {
                // Do something when calibration is true
                Debug.Log("Calibration is true, set button active to false");
                calibrationButton.SetActive(false);
                calibrationBool = true;
            }
            else
            {
                // Do something when calibration is false
                Debug.Log("Calibration is false, set button active to true");
                calibrationButton.SetActive(true);
                calibrationBool = false;
            }
        }
        Debug.Log("Saving Json in unity editor and performing setting up the scene");
        Debug.Log("calibration: " + configData.data["calibration"]);
        #endif
        
        #if WINDOWS_UWP
            // Define folder/file paths and create them if needed, else open.
            var musicLibrary = KnownFolders.MusicLibrary;
            var hintsFolder = await musicLibrary.CreateFolderAsync("HINTS", CreationCollisionOption.OpenIfExists);
            var configFile = await hintsFolder.CreateFileAsync("Config.json", CreationCollisionOption.OpenIfExists);
            configFilePath = configFile.Path;

            // create new dictionary
            configData = new ConfigData();
            configData.data = new Dictionary<string, bool>();

            // if json is empty or null, create/write to file, set calibration(Bool) to false.
            string json = await FileIO.ReadTextAsync(configFile);
            if (string.IsNullOrEmpty(json))
            {
                configData.data["calibration"] = false;
                json = JsonConvert.SerializeObject(configData);
                await FileIO.WriteTextAsync(configFile, json);
                calibrationBool = false;
            }
            // if json is not empty, read the values from it and set button/bool ackordingly.
            else
            {
                configData = JsonConvert.DeserializeObject<ConfigData>(json);
                if (configData.data.ContainsKey("calibration") && configData.data["calibration"])
                {
                    // Do something when calibration is true
                    calibrationButton.SetActive(false);
                    calibrationBool = true;
                }
                else
                {
                    calibrationButton.SetActive(true);
                    calibrationBool = false;
                }
            }
        #endif
    }

    public void resetButtons() //better to do async, but i dont care in this case, I want to guarantee writing to json file is finished first.
    {
        // set config calibration value to false
        #if UNITY_EDITOR
            // Update the calibration value in the configData dictionary
            if (configData.data.ContainsKey("calibration"))
            {
                configData.data["calibration"] = false;
            }
            else
            {
                configData.data.Add("calibration", false);
            }
            // Write the updated dictionary back to the JSON file
            string json = JsonConvert.SerializeObject(configData);
            File.WriteAllText(configFilePath, json);
        #endif

        #if WINDOWS_UWP
            if (configData != null && !string.IsNullOrEmpty(configFilePath))
            {
                configData.data["calibration"] = false;
                string json = JsonConvert.SerializeObject(configData);
                StorageFile configFile = StorageFile.GetFileFromPathAsync(configFilePath).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                FileIO.WriteTextAsync(configFile, json).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            }
        #endif

        // set the bools and timers and state back to original start values
        savedWavBool = false;
        calibrationBool = false; startBool = false;
        startLogicBool = false; startTime = Time.time;
        isStartSoundPlayingBool = false; startMessageBool = false; startMeasureTimer = 0f;
        isRaumSoundPlayingBool = false; raumMessageBool = false; raumMeasureTimer = 0f;
        isStillSoundPlayingBool = false; stillMessageBool = false; stillMeasureTimer = 0f;
        isNaseSoundPlayingBool = false; naseMessageBool = false; naseMeasureTimer = 0f; 
        isLinksSoundPlayingBool = false; linksMessageBool = false; linksMeasureTimer = 0f; 
        isRechtsSoundPlayingBool = false; rechtsMessageBool = false; rechtsMeasureTimer = 0f; 
        isDeckeSoundPlayingBool = false; deckeMessageBool = false; deckeMeasureTimer = 0f; 
        isBodenSoundPlayingBool = false; bodenMessageBool = false; bodenMeasureTimer = 0f; 
        blingAudioSoundBool = false; delayBlingMeasureTimer = 0f; 
        isSaveScheduledBool = false;
        j = 0;
        CurrentState = State.StartLogic;
        // clean message for saving
        timeStampStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        timeStampStart = timeStampStart.Replace("-", "_").Replace(" ", "_").Replace(":", "_");
        longMessageForSaving.Clear();
        longMessageForSaving.Append("timestamp, " +     
            "worldLeftEyePosition, worldLeftEyeDirection, worldRightEyePosition, worldRightEyeDirection, worldCombinedEyePosition, worldCombinedEyeDirection, " +
            "cameraLeftEyePosition, cameraLeftEyeDirection, cameraRightEyePosition, cameraRightEyeDirection, cameraCombinedEyePosition, cameraCombinedEyeDirection, " +
            "headPosition, headEulerAngles, headQuaternion " +
            "\n"
        );
        
        // set both buttons to active
        calibrationButton.SetActive(true);
        startButton.SetActive(true);
    }

}
