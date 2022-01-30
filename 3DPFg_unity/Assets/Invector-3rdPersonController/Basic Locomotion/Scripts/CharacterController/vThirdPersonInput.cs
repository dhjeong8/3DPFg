using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System;
using System.IO;

namespace Invector.vCharacterController
{
    [vClassHeader("Input Manager", iconName = "inputIcon")]
    public class vThirdPersonInput : vMonoBehaviour, vIAnimatorMoveReceiver
    {
        public delegate void OnUpdateEvent();
        public event OnUpdateEvent onUpdate;
        public event OnUpdateEvent onLateUpdate;
        public event OnUpdateEvent onFixedUpdate;
        public event OnUpdateEvent onAnimatorMove;

        #region Variables        

        [vEditorToolbar("Inputs")]
        [vHelpBox("Check these options if you need to use the mouse cursor, ex: <b>2.5D, Topdown or Mobile</b>", vHelpBoxAttribute.MessageType.Info)]
        public int caseNum;
        public bool unlockCursorOnStart = false;
        public bool showCursorOnStart = false;
        [vHelpBox("PC only - use it to toggle between run/walk", vHelpBoxAttribute.MessageType.Info)]
        public KeyCode toggleWalk = KeyCode.CapsLock;
        private bool _toogleWalk;

        float timer;
        float cameraTimer;
        float cameraWaitingTime;


        [Header("Movement Input")]
        public GenericInput horizontalInput = new GenericInput("Horizontal", "LeftAnalogHorizontal", "Horizontal");
        public GenericInput verticallInput = new GenericInput("Vertical", "LeftAnalogVertical", "Vertical");
        public GenericInput sprintInput = new GenericInput("LeftShift", "LeftStickClick", "LeftStickClick");
        public GenericInput crouchInput = new GenericInput("C", "Y", "Y");
        public GenericInput strafeInput = new GenericInput("Tab", "RightStickClick", "RightStickClick");
        public GenericInput jumpInput = new GenericInput("Space", "X", "X");
        public GenericInput rollInput = new GenericInput("Q", "B", "B");

        [HideInInspector] public bool lockInput;

        [vEditorToolbar("Camera Settings")]
        public bool lockCameraInput;
        public bool invertCameraInputVertical, invertCameraInputHorizontal;
        [vEditorToolbar("Inputs")]
        [Header("Camera Input")]
        public GenericInput rotateCameraXInput = new GenericInput("Mouse X", "RightAnalogHorizontal", "Mouse X");
        public GenericInput rotateCameraYInput = new GenericInput("Mouse Y", "RightAnalogVertical", "Mouse Y");
        public GenericInput cameraZoomInput = new GenericInput("Mouse ScrollWheel", "", "");

        [vEditorToolbar("Events")]
        public UnityEvent OnLockCamera;
        public UnityEvent OnUnlockCamera;
        public UnityEvent onEnableAnimatorMove = new UnityEvent();
        public UnityEvent onDisableDisableAnimatorMove = new UnityEvent();

        [HideInInspector]
        public vCamera.vThirdPersonCamera tpCamera;         // access tpCamera info
        [HideInInspector]
        public bool ignoreTpCamera;                         // controls whether update the cameraStates of not                
        [HideInInspector]
        public string customCameraState;                    // generic string to change the CameraState
        [HideInInspector]
        public string customlookAtPoint;                    // generic string to change the CameraPoint of the Fixed Point Mode
        [HideInInspector]
        public bool changeCameraState;                      // generic bool to change the CameraState
        [HideInInspector]
        public bool smoothCameraState;                      // generic bool to know if the state will change with or without lerp
        [HideInInspector]
        public vThirdPersonController cc;                   // access the ThirdPersonController component
        [HideInInspector]
        public vHUDController hud;                          // acess vHUDController component
        protected bool updateIK = false;
        protected bool isInit;
        [HideInInspector] public bool lockMoveInput;
        protected InputDevice inputDevice { get { return vInput.instance.inputDevice; } }

        public float bestheights = 0f;
        protected Camera _cameraMain;
        protected bool withoutMainCamera;
        internal bool lockUpdateMoveDirection;                // lock the method UpdateMoveDirection
        protected Terrain t;

        public float horizontalKey;
        public float verticalKey;
        public bool doSprint;
        public bool doJump;
        public int count;
        public String new_key;
        public String last_key;
        public StreamWriter sw;
        public Vector3 startPos;
        public Transform GoalPoint;
        public Vector3 goalPos;
        public Vector3 tempPos;
        public Vector3 velocity;
        public Camera cameraMain
        {
            get
            {
                if (!_cameraMain && !withoutMainCamera)
                {
                    if (!Camera.main)
                    {
                        Debug.Log("Missing a Camera with the tag MainCamera, please add one.");
                        withoutMainCamera = true;
                    }
                    else
                    {
                        _cameraMain = Camera.main;
                        cc.rotateTarget = _cameraMain.transform;
                    }
                }
                return _cameraMain;
            }
            set
            {
                _cameraMain = value;
            }
        }

        public Animator animator
        {
            get
            {
                if (cc == null)
                {
                    cc = GetComponent<vThirdPersonController>();
                }

                if (cc.animator == null)
                {
                    return GetComponent<Animator>();
                }

                return cc.animator;
            }
        }

        #endregion

        #region Initialize Character, Camera & HUD when LoadScene

        protected virtual void Start()
        {
            timer = 0f;


            t = GameObject.Find("land").GetComponent<Terrain>();
            Debug.Log(t);
            var GoalPoint = GameObject.Find("GoalPoint").GetComponent<Rigidbody>();
            horizontalKey = verticallInput.GetAxisRaw();
            verticalKey = horizontalInput.GetAxisRaw();
            doSprint = sprintInput.GetButton();
            doJump = jumpInput.GetButtonDown();


            cc = GetComponent<vThirdPersonController>();
            //startPos = cc.transform.position;
            //string filepth = "Assets/inputTxt/input"+caseNum;
            // if(false == File.Exists(filepth)){
            //     sw = new StreamWriter(filepth + ".txt");
            // }
            if (cc != null)
            {
                cc.Init();
            }

            StartCoroutine(CharacterInit());

            ShowCursor(showCursorOnStart);
            LockCursor(unlockCursorOnStart);
            EnableOnAnimatorMove();
        }

        protected virtual IEnumerator CharacterInit()
        {
            FindCamera();
            yield return new WaitForEndOfFrame();
            FindHUD();
        }

        public virtual void FindHUD()
        {
            if (hud == null && vHUDController.instance != null)
            {
                hud = vHUDController.instance;
                hud.Init(cc);
            }
        }

        public virtual void FindCamera()
        {
            var tpCameras = FindObjectsOfType<vCamera.vThirdPersonCamera>();

            if (tpCameras.Length > 1)
            {
                tpCamera = System.Array.Find(tpCameras, tp => !tp.isInit);

                if (tpCamera == null)
                {
                    tpCamera = tpCameras[0];
                }

                if (tpCamera != null)
                {
                    for (int i = 0; i < tpCameras.Length; i++)
                    {
                        if (tpCamera != tpCameras[i])
                        {
                            Destroy(tpCameras[i].gameObject);
                        }
                    }
                }
            }
            else if (tpCameras.Length == 1)
            {
                tpCamera = tpCameras[0];
            }

            if (tpCamera && tpCamera.mainTarget != transform)
            {
                tpCamera.SetMainTarget(this.transform);
            }
        }

        #endregion

        protected virtual void LateUpdate()
        {
            if (cc == null || Time.timeScale == 0)
            {
                return;
            }

            if (!updateIK)
            {
                return;
            }

            if (onLateUpdate != null)
            {
                onLateUpdate.Invoke();
            }

            CameraInput();                      // update camera input
            UpdateCameraStates();               // update camera states                        
            updateIK = false;
        }

        protected virtual void FixedUpdate()
        {
            if (onFixedUpdate != null)
            {
                onFixedUpdate.Invoke();
            }

            Physics.SyncTransforms();
            cc.UpdateMotor();                                                   // handle the ThirdPersonMotor methods            
            cc.ControlLocomotionType();                                         // handle the controller locomotion type and movespeed   
            ControlRotation();
            cc.UpdateAnimator();                                                // handle the ThirdPersonAnimator methods
            updateIK = true;
        }
        protected virtual void Awake(){
            cc = GetComponent<vThirdPersonController>();
            tempPos = cc.transform.position;
        }
        protected virtual void Update()
        {
            if (cc == null || Time.timeScale == 0)
            {
                return;
            }

            if (onUpdate != null)
            {
                onUpdate.Invoke();
            }
            InputHandle();
            // new_key = BehaviorRecord();
            // if(new_key != last_key){
            //     getVelocity();
            //     sw.WriteLine(new_key);
            //     timer = 0;
            // }
            // last_key = new_key;

            
            if(count == 0){
                InputHandle();
                count++;
            }
            timer += Time.deltaTime;
            // if(timer>waitingTime){
            //     //InputHandle();
            //     getVelocity();
            //     sw.WriteLine(new_key);
            //     timer = 0;
            // }
            if(cc.transform.position.y > bestheights){
                bestheights = cc.transform.position.y;
            }
            /*if(!cc.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Airborne.Falling.Falling")){
                horizontalKey = verticallInput.GetAxisRaw();
                verticalKey = horizontalInput.GetAxisRaw();
            }*/
            //doSprint = sprintInput.GetButton();
            //doJump = jumpInput.GetButtonDown();
                
            InputHandle();                      // update input methods                        
            UpdateHUD();                        // update hud graphics
            // if(Input.GetKey(KeyCode.Q)){
            //     sw.WriteLine(cc.transform.position);
            //     sw.WriteLine(startPos);
            //     sw.Flush();
            //     sw.Close();
            //     Debug.Log("input txt complete");
            // }            
        }

        // public void getVelocity(){
        //     velocity = (cc.transform.position - tempPos) / timer;
        //     Debug.Log(cc.transform.position - tempPos);
        //     tempPos = cc.transform.position;
        //     //Debug.Log(tempPos);
        //     Debug.Log("velocity vector: "+velocity);

        // }

        ///reset the way to get input to directly control at AddOn(freeclimb, parachute)
        /*public void keyInputforAddon(){
            horizontalKey = verticallInput.GetAxisRaw();
            verticalKey = horizontalInput.GetAxisRaw();
            doJump = jumpInput.GetButtonDown();
        }*/

        public virtual void OnAnimatorMoveEvent()
        {
            if (cc == null)
            {
                return;
            }

            cc.ControlAnimatorRootMotion();
            if (onAnimatorMove != null)
            {
                onAnimatorMove.Invoke();
            }
        }

        #region Generic Methods
        // you can call this methods anywhere in the inspector or third party assets to have better control of the controller or cutscenes

        /// <summary>
        /// Lock all Basic  Input from the Player
        /// </summary>
        /// <param name="value"></param>
        public virtual void SetLockBasicInput(bool value)
        {
            lockInput = value;
            if (value)
            {
                cc.input = Vector2.zero;
                cc.isSprinting = false;
                cc.animator.SetFloat("InputHorizontal", 0, 0.25f, Time.deltaTime);
                cc.animator.SetFloat("InputVertical", 0, 0.25f, Time.deltaTime);
                cc.animator.SetFloat("InputMagnitude", 0, 0.25f, Time.deltaTime);
            }
        }

        /// <summary>
        /// Lock all Inputs 
        /// </summary>
        /// <param name="value"></param>
        public virtual void SetLockAllInput(bool value)
        {
            SetLockBasicInput(value);
        }

        /// <summary>
        /// Show/Hide Cursor
        /// </summary>
        /// <param name="value"></param>
        public virtual void ShowCursor(bool value)
        {
            Cursor.visible = value;
        }

        /// <summary>
        /// Lock/Unlock the cursor to the center of screen
        /// </summary>
        /// <param name="value"></param>
        public virtual void LockCursor(bool value)
        {
            if (!value)
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
            }
        }

        /// <summary>
        /// Lock the Camera Input
        /// </summary>
        /// <param name="value"></param>
        public virtual void SetLockCameraInput(bool value)
        {
            lockCameraInput = value;

            if (lockCameraInput)
            {
                OnLockCamera.Invoke();
            }
            else
            {
                OnUnlockCamera.Invoke();
            }
        }

        /// <summary>
        /// If you're using the MoveCharacter method with a custom targetDirection, check this true to align the character with your custom targetDirection
        /// </summary>
        /// <param name="value"></param>
        public virtual void SetLockUpdateMoveDirection(bool value)
        {
            lockUpdateMoveDirection = value;
        }

        /// <summary>
        /// Limits the character to walk only, useful for cutscenes and 'indoor' areas
        /// </summary>
        /// <param name="value"></param>
        public virtual void SetWalkByDefault(bool value)
        {
            cc.freeSpeed.walkByDefault = value;
            cc.strafeSpeed.walkByDefault = value;
        }

        /// <summary>
        /// Set the character to Strafe Locomotion
        /// </summary>
        /// <param name="value"></param>
        public virtual void SetStrafeLocomotion(bool value)
        {
            cc.lockInStrafe = value;
            cc.isStrafing = value;
        }

        /// <summary>
        /// OnAnimatorMove Event Sender 
        /// </summary>
        internal virtual vAnimatorMoveSender animatorMoveSender { get; set; }

        /// <summary>
        /// Use Animator Move Event Sender <seealso cref="vAnimatorMoveSender"/>
        /// </summary>
        protected bool _useAnimatorMove { get; set; }

        /// <summary>
        /// Check if OnAnimatorMove is Enabled
        /// </summary>
        public virtual bool UseAnimatorMove
        {
            get
            {
                return _useAnimatorMove;
            }
            set
            {

                if (_useAnimatorMove != value)
                {
                    if (value)
                    {
                        animatorMoveSender = gameObject.AddComponent<vAnimatorMoveSender>();
                        onEnableAnimatorMove?.Invoke();
                    }
                    else
                    {
                        if (animatorMoveSender)
                        {
                            Destroy(animatorMoveSender);
                        }

                        onEnableAnimatorMove?.Invoke();
                    }
                }
                _useAnimatorMove = value;
            }
        }

        /// <summary>
        /// Enable OnAnimatorMove event
        /// </summary>
        public virtual void EnableOnAnimatorMove()
        {
            UseAnimatorMove = true;
        }

        /// <summary>
        /// Disable OnAnimatorMove event
        /// </summary>
        public virtual void DisableOnAnimatorMove()
        {
            UseAnimatorMove = false;
        }

        #endregion

        #region Basic Locomotion Inputs

        protected virtual void InputHandle()
        {
            if (lockInput || cc.ragdolled)
            {
                return;
            }
            MoveInput();
            SprintInput();
            CrouchInput();
            StrafeInput();
            JumpInput();
            RollInput();
            //getHeights();
            //BehaviorSelector();

        }

        ///get map heights, if you progress forward vector
        public virtual void getHeights(){
            int xSize = 10, zSize = 10;
            float[,] heights_at_position = new float[xSize, zSize];
            Vector3 dir_vec = cc.transform.position - tpCamera.transform.position;
            dir_vec = dir_vec.normalized;

            var worldPos = cc.transform.position;
            int mapX = (int)(((worldPos.x - t.transform.position.x) / t.terrainData.size.x) * t.terrainData.alphamapWidth);
            int mapZ = (int)(((worldPos.z - t.transform.position.z) / t.terrainData.size.z) * t.terrainData.alphamapHeight);
            //float mapX = cc.transform.position.x;
            //float mapZ = cc.transform.position.z;
            float dx = 5f/10f/dir_vec.x, dz = 5f/10f/dir_vec.z;
            string str_debug = "";
            for(int i = 0; i < 10; ++i) {
                for(int j = 0; j < 10; ++j) {
                    heights_at_position[i,j] = t.terrainData.GetHeight((int)(mapX + i * dx), (int)(mapZ + j * dz));
                    str_debug += heights_at_position[i, j].ToString() + " ";
                }
                str_debug += "\n";
            }
            //return heights_at_position;
            
            Debug.Log(str_debug);
        }

        ///change the input by keyInput.txt action state
        public void readState(String key_input)
        {

            if(String.Equals(key_input, "Wait")){
                doJump = false;
                doSprint = false;
                verticalKey = 0;
                horizontalKey =0;
            }else{
                if(key_input.Contains("j")) doJump = true;
                else    doJump = false;
                if(key_input.Contains("s")) doSprint = true;
                else    doSprint = false;
                if(key_input.Contains("W")) horizontalKey = 1;
                else if(key_input.Contains("S")) horizontalKey = -1;
                else horizontalKey = 0;
                if(key_input.Contains("A")) verticalKey = -1;
                else if(key_input.Contains("D")) verticalKey = 1;
                else verticalKey = 0;
                //Debug.Log("j: "+doJump+" s: "+doSprint+" AD: "+verticalKey+" WS: "+horizontalKey);
            }

        }

        ///make the random keyInput in Unity and move by that
        public virtual void BehaviorSelector()
        {
            Vector3 dir_vec = cc.transform.position - tpCamera.transform.position;
            if(!cc.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Airborne.Falling.Falling")){
                horizontalKey = horizontalInput.GetAxisRaw();
                verticalKey = verticallInput.GetAxisRaw();
            }
            doSprint = sprintInput.GetButton();
            doJump = jumpInput.GetButtonDown();
            //int num = 0;
            /*if(key_input.Contains("j")) doJump = true;
            else    doJump = false;
            if(key_input.Contains("s")) doSprint = true;
            else    doSprint = false;
            if(key_input.Contains("W")) verticalKey = 1;
            else    verticalKey = 0;
            if(key_input.Contains("S"))    verticalKey = -1;
            else    verticalKey = 0;
            if(key_input.Contains("A"))    horizontalKey = -1;
            if(key_input.Contains("D"))    horizontalKey = 1;
*/
            //String s;
            //switch(s){
            //    case 
            //}
            //num = UnityEngine.Random.Range(1,33);
            /*switch(num){
                case 1:     
                    Debug.Log("D");
                    horizontalKey = 1;
                    verticalKey = 0;
                    doSprint = false;
                    doJump = false;
                    break;
                case 2:   
                    Debug.Log("A");      
                    horizontalKey = -1;
                    verticalKey = 0;
                    doSprint = false;
                    doJump = false;
                    break;
                case 3:      
                    Debug.Log("W");  
                    horizontalKey = 0; 
                    verticalKey = 1;
                    doSprint = false;
                    doJump = false;
                    break;
                case 4:         
                    Debug.Log("S");
                    horizontalKey = 0;
                    verticalKey = -1;
                    doSprint = false;
                    doJump = false;
                    break;
                case 5:
                    Debug.Log("WD");
                    horizontalKey = 1;
                    verticalKey = 1;
                    doSprint = false;
                    doJump = false;
                    break;
                case 6:
                    Debug.Log("SD");
                    horizontalKey = 1;
                    verticalKey = -1;
                    doSprint = false;
                    doJump = false;
                    break;
                case 7:
                    Debug.Log("WA");
                    horizontalKey = -1;
                    verticalKey = 1;
                    doSprint = false;
                    doJump = false;
                    break;
                case 8:
                    Debug.Log("SA");
                    horizontalKey = -1;
                    verticalKey = -1;
                    doSprint = false;
                    doJump = false;
                    break;
                case 9:     
                    Debug.Log("Shift+D");
                    horizontalKey = 1;
                    verticalKey = 0;
                    doSprint = true;
                    doJump = false;
                    break;
                case 10:   
                    Debug.Log("Shift+A");      
                    horizontalKey = -1;
                    verticalKey = 0;
                    doSprint = true;
                    doJump = false;
                    break;
                case 11:      
                    Debug.Log("Shift+W");  
                    horizontalKey = 0; 
                    verticalKey = 1;
                    doSprint = true;
                    doJump = false;
                    break;
                case 12:         
                    Debug.Log("Shift+S");
                    horizontalKey = 0;
                    verticalKey = -1;
                    doSprint = true;
                    doJump = false;
                    break;
                case 13:
                    Debug.Log("Shift+WD");
                    horizontalKey = 1;
                    verticalKey = 1;
                    doSprint = true;
                    doJump = false;
                    break;
                case 14:
                    Debug.Log("Shift+SD");
                    horizontalKey = 1;
                    verticalKey = -1;
                    doSprint = true;
                    doJump = false;
                    break;
                case 15:
                    Debug.Log("Shift+WA");
                    horizontalKey = -1;
                    verticalKey = 1;
                    doSprint = true;
                    doJump = false;
                    break;
                case 16:
                    Debug.Log("Shift+SA");
                    horizontalKey = -1;
                    verticalKey = -1;
                    doSprint = true;
                    doJump = false;
                    break;
                case 17:     
                    Debug.Log("Space+D");
                    horizontalKey = 1;
                    verticalKey = 0;
                    doSprint = false;
                    doJump = true;
                    break;
                case 18:         
                    Debug.Log("Space+A");
                    horizontalKey = -1;
                    verticalKey = 0;
                    doSprint = false;
                    doJump = true;
                    break;
                case 19:        
                    Debug.Log("Space+W");
                    horizontalKey = 0; 
                    verticalKey = 1;
                    doSprint = false;
                    doJump = true;
                    break;
                case 20:         
                    Debug.Log("Space+S");
                    horizontalKey = 0;
                    verticalKey = -1;
                    doSprint = false;
                    doJump = true;
                    break;
                case 21:
                    Debug.Log("Space+WD");
                    horizontalKey = 1;
                    verticalKey = 1;
                    doSprint = false;
                    doJump = true;
                    break;
                case 22:
                    Debug.Log("Space+SD");
                    horizontalKey = 1;
                    verticalKey = -1;
                    doSprint = false;
                    doJump = true;
                    break;
                case 23:
                    Debug.Log("Space+WA");
                    horizontalKey = -1;
                    verticalKey = 1;
                    doSprint = false;
                    doJump = true;
                    break;
                case 24:
                    Debug.Log("Space+SA");
                    horizontalKey = -1;
                    verticalKey = -1;
                    doSprint = false;
                    doJump = true;
                    break;
                case 25:     
                    Debug.Log("Shift+Space+D");
                    horizontalKey = 1;
                    verticalKey = 0;
                    doSprint = true;
                    doJump = true;
                    break;
                case 26:        
                    Debug.Log("Shift+Space+A"); 
                    horizontalKey = -1;
                    verticalKey = 0;
                    doSprint = true;
                    doJump = true;
                    break;
                case 27:        
                    Debug.Log("Shift+Space+W");
                    horizontalKey = 0; 
                    verticalKey = 1;
                    doSprint = true;
                    doJump = true;
                    break;
                case 28:         
                    Debug.Log("Shift+Space+S");
                    horizontalKey = 0;
                    verticalKey = -1;
                    doSprint = true;
                    doJump = true;
                    break;
                case 29:
                    Debug.Log("Shift+Space+WD");
                    horizontalKey = 1;
                    verticalKey = 1;
                    doSprint = true;
                    doJump = true;
                    break;
                case 30:
                    Debug.Log("Shift+Space+SD");
                    horizontalKey = 1;
                    verticalKey = -1;
                    doSprint = true;
                    doJump = true;
                    break;
                case 31:
                    Debug.Log("Shift+Space+WA");
                    horizontalKey = -1;
                    verticalKey = 1;
                    doSprint = true;
                    doJump = true;
                    break;
                case 32:
                    Debug.Log("Shift+Space+SA");
                    horizontalKey = -1;
                    verticalKey = -1;
                    doSprint = true;
                    doJump = true;
                    break;
                case 33:
                    Debug.Log("Stop");
                    horizontalKey = 0;
                    verticalKey = 0;
                    doSprint = false;
                    doJump = false;
                    break;
                default:
                    break;
            }*/
        
        }


        ///Change input condition keyboard and mouse input to scriptable(horizontalKey, verticalKey, doJump, doSprint)
        public virtual void MoveInput()
        {
            if (!lockMoveInput)
            {
                // gets input
                //cc.input.x = horizontalInput.GetAxisRaw();
                cc.input.x = horizontalKey;
                //cc.input.x = 1;
                cc.input.z = verticalKey;
                //cc.input.z = verticallInput.GetAxisRaw();
                
            }

            if (true)
            {
                _toogleWalk = !_toogleWalk;
                SetWalkByDefault(false);
            }

            cc.ControlKeepDirection();
        }

        public virtual void ControlRotation()
        {
            if (cameraMain && !lockUpdateMoveDirection)
            {
                if (!cc.keepDirection)
                {
                    cc.UpdateMoveDirection(cameraMain.transform);
                }
            }

            if (tpCamera != null && tpCamera.lockTarget && cc.isStrafing)
            {
                cc.RotateToPosition(tpCamera.lockTarget.position);          // rotate the character to a specific target
            }
            else
            {
                cc.ControlRotationType();                                   // handle the controller rotation type (strafe or free)
            }
        }

        protected virtual void StrafeInput()
        {
            if (strafeInput.GetButtonDown())
            {
                cc.Strafe();
            }
        }

        protected virtual void SprintInput()
        {
            if (sprintInput.useInput)
            {
                cc.Sprint(cc.useContinuousSprint ? sprintInput.GetButtonDown() : doSprint);
            }
        }

        protected virtual void CrouchInput()
        {
            cc.AutoCrouch();

            if (crouchInput.useInput && crouchInput.GetButtonDown())
            {
                cc.Crouch();
            }
        }

        /// <summary>
        /// Conditions to trigger the Jump animation & behavior
        /// </summary>
        /// <returns></returns>
        protected virtual bool JumpConditions()
        {
            return !cc.customAction && !cc.isCrouching && cc.isGrounded && cc.GroundAngle() < cc.slopeLimit && cc.currentStamina >= cc.jumpStamina && !cc.isJumping && !cc.isRolling;
        }

        /// <summary>
        /// Input to trigger the Jump 
        /// </summary>
        protected virtual void JumpInput()
        {
            if (doJump && JumpConditions())
            {
                cc.Jump(true);
                doJump = false;
            }
        }

        /// <summary>
        /// Conditions to trigger the Roll animation & behavior
        /// </summary>
        /// <returns></returns>
        protected virtual bool RollConditions()
        {
            return (!cc.isRolling || cc.canRollAgain) && cc.input != Vector3.zero && !cc.customAction && cc.isGrounded && cc.currentStamina > cc.rollStamina && !cc.isJumping;
        }

        /// <summary>
        /// Input to trigger the Roll
        /// </summary>
        protected virtual void RollInput()
        {
            if (rollInput.GetButtonDown() && RollConditions())
            {
                cc.Roll();
            }
        }

        #endregion       

        #region Camera Methods

        public virtual void CameraInput()
        {
            if (!cameraMain)
            {
                return;
            }

            if (tpCamera == null)
            {
                return;
            }

            var Y = lockCameraInput ? 0f : rotateCameraYInput.GetAxis();
            var X = lockCameraInput ? 0f : rotateCameraXInput.GetAxis();
            if (invertCameraInputHorizontal)
            {
                X *= -1;
            }

            if (invertCameraInputVertical)
            {
                Y *= -1;
            }

            var zoom = cameraZoomInput.GetAxis();

            tpCamera.RotateCamera(X, Y);
            if (!lockCameraInput)
            {
                tpCamera.Zoom(zoom);
            }
        }

        public virtual void UpdateCameraStates()
        {
            // CAMERA STATE - you can change the CameraState here, the bool means if you want lerp of not, make sure to use the same CameraState String that you named on TPCameraListData
            if (ignoreTpCamera)
            {
                return;
            }

            if (tpCamera == null)
            {
                tpCamera = FindObjectOfType<vCamera.vThirdPersonCamera>();
                if (tpCamera == null)
                {
                    return;
                }

                if (tpCamera)
                {
                    tpCamera.SetMainTarget(this.transform);
                    tpCamera.Init();
                }
            }

            if (changeCameraState)
            {
                tpCamera.ChangeState(customCameraState, customlookAtPoint, smoothCameraState);
            }
            else if (cc.isCrouching)
            {
                tpCamera.ChangeState("Crouch", true);
            }
            else if (cc.isStrafing)
            {
                tpCamera.ChangeState("Strafing", true);
            }
            else
            {
                tpCamera.ChangeState("Default", true);
            }
        }

        public virtual void ChangeCameraState(string cameraState, bool useLerp = true)
        {
            if (useLerp)
            {
                ChangeCameraStateWithLerp(cameraState);
            }
            else
            {
                ChangeCameraStateNoLerp(cameraState);
            }
        }

        public virtual void ResetCameraAngle()
        {
            if (tpCamera)
            {
                tpCamera.ResetAngle();
            }
        }

        public virtual void ChangeCameraStateWithLerp(string cameraState)
        {
            changeCameraState = true;
            customCameraState = cameraState;
            smoothCameraState = true;
        }

        public virtual void ChangeCameraStateNoLerp(string cameraState)
        {
            changeCameraState = true;
            customCameraState = cameraState;
            smoothCameraState = false;
        }

        public virtual void ResetCameraState()
        {
            changeCameraState = false;
            customCameraState = string.Empty;
        }

        #endregion

        #region HUD       

        public virtual void UpdateHUD()
        {
            if (hud == null)
            {
                if (vHUDController.instance != null)
                {
                    hud = vHUDController.instance;
                    hud.Init(cc);
                }
                else
                {
                    return;
                }
            }

            hud.UpdateHUD(cc);
        }

        #endregion
    }

    /// <summary>
    /// Interface to receive events from <seealso cref="vAnimatorMoveSender"/>
    /// </summary>
    public interface vIAnimatorMoveReceiver
    {
        /// <summary>
        /// Check if Component is Enabled
        /// </summary>
        bool enabled { get; set; }
        /// <summary>
        /// Method Called from <seealso cref="vAnimatorMoveSender"/>
        /// </summary>
        void OnAnimatorMoveEvent();
    }

    /// <summary>
    /// OnAnimatorMove Event Sender 
    /// </summary>
    class vAnimatorMoveSender : MonoBehaviour
    {
        private void Awake()
        {
            ///Hide in Inpector
            this.hideFlags = HideFlags.HideInInspector;
            vIAnimatorMoveReceiver[] animatorMoves = GetComponents<vIAnimatorMoveReceiver>();
            for (int i = 0; i < animatorMoves.Length; i++)
            {
                var receiver = animatorMoves[i];
                animatorMoveEvent += () =>
                {
                    if (receiver.enabled)
                    {
                        receiver.OnAnimatorMoveEvent();
                    }
                };
            }
        }

        /// <summary>
        /// AnimatorMove event called using  default unity OnAnimatorMove
        /// </summary>
        public System.Action animatorMoveEvent;

        private void OnAnimatorMove()
        {
            animatorMoveEvent?.Invoke();
        }
    }
}