using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.IO;

namespace Invector
{
    using UnityEngine.Events;
    using vCharacterController;
    [vClassHeader("Simple GameController Example", openClose = false)]
    public class vGameController : vMonoBehaviour
    {
        [System.Serializable]
        public class OnRealoadGame : UnityEngine.Events.UnityEvent { }
        [vHelpBox("Assign your Character Prefab to be instantiate at the SpawnPoint, leave it unassigned to Restart the Scene instead")]
        public GameObject playerPrefab;
        [vHelpBox("Assign a empty transform to spawn the Player to a specific location")]
        public Transform spawnPoint;
        [vHelpBox("Time to wait until the scene restart or the player will be spawned again")]
        public float respawnTimer = 4f;
        [vHelpBox("Check this if you want to destroy the dead body after the respawn")]
        public bool destroyBodyAfterDead;
        [vHelpBox("Display a message using the FadeText UI")]
        public bool displayInfoInFadeText = true;

        [HideInInspector]
        public OnRealoadGame OnReloadGame = new OnRealoadGame();
        [HideInInspector]
        public GameObject currentPlayer;
        private vThirdPersonController currentController;
        public static vGameController instance;
        private GameObject oldPlayer;
        public Transform goalPoint;
        public UnityEvent onSpawn;
        public Text recordText;
        public GameObject gameoverText;
        [HideInInspector]
        private float findTime;
        [HideInInspector]
        public float bestTime;
        [HideInInspector]
        public int count;
        [HideInInspector]
        public Vector3 goalPos;
        public int caseNum;
        [HideInInspector]
        public int randomGoalx;
        [HideInInspector]
        public int randomGoalz;
        protected Terrain t;
        [HideInInspector]
        public String new_key;
        [HideInInspector]
        public String last_key;
        [HideInInspector]
        float timer;
        [HideInInspector]
        float waitingTime;
        public StreamWriter sw;
        public StreamReader sr;

        public GenericInput horizontalInput = new GenericInput("Horizontal", "LeftAnalogHorizontal", "Horizontal");
        public GenericInput verticallInput = new GenericInput("Vertical", "LeftAnalogVertical", "Vertical");
        public GenericInput sprintInput = new GenericInput("LeftShift", "LeftStickClick", "LeftStickClick");
        public GenericInput jumpInput = new GenericInput("Space", "X", "X");

        [HideInInspector]
        public float horizontalKey;
        [HideInInspector]
        public float verticalKey;
        [HideInInspector]
        public bool doSprint;
        [HideInInspector]
        public bool doJump;
        [HideInInspector]
        public Vector3 startPos;
        public int actionNum = 0;

        Dictionary<string, int> angles = new Dictionary<string, int>(){
            {"W",0},
            {"D",90},
            {"S",180},
            {"A",270},
            {"WD",45},
            {"SD",135},
            {"SA",225},
            {"WA",315}
        };
        String[] actionNumber = new String[]    {"wait", "W", "A", "S", "D", "WA", "WD", "SD", "SA", "Ws", "As", "Ss", "Ds",
            "WAs", "WDs", "SDs", "SAs", "Wj", "Aj", "Aj", "Sj", "Dj", "WAj", "WDj", "SDj", "SAj", "Wsj", "Asj", "Ssj", "Dsj", "WAsj", "WDsj",
            "SDsj", "SAsj", "j"};

        List<string> action_list = new List<string>();
        List<string> script_position_list = new List<string>();

        public vCharacterController.vThirdPersonInput tp_input;

        
        protected virtual void Start()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(this.gameObject);
                this.gameObject.name = gameObject.name + " Instance";
            }
            else
            {
                Destroy(this.gameObject);
                return;
            }

            SceneManager.sceneLoaded += OnLevelFinishedLoading;
            if (displayInfoInFadeText && vHUDController.instance)
            {
                vHUDController.instance.ShowText("Init Scene");
            }
            tp_input = FindObjectOfType<vCharacterController.vThirdPersonInput>();

            FindPlayer();

            //input
            

            timer = 0f;
            waitingTime = 1f;
            startPos = currentController.transform.position;
            string filepth = "Assets/inputTxt/input"+caseNum;
            
            if(false == File.Exists(filepth)){
                sw = new StreamWriter(filepth + ".txt");
            }
            reader();
            horizontalKey = horizontalInput.GetAxisRaw();
            verticalKey = verticallInput.GetAxisRaw();
            doSprint = sprintInput.GetButton();
            doJump = jumpInput.GetButtonDown();
            t = GameObject.Find("land").GetComponent<Terrain>();

            randomGoalx = UnityEngine.Random.Range(1,100);
            randomGoalz = UnityEngine.Random.Range(1,100);

            var goalPoint = GameObject.Find("GoalPoint").GetComponent<Rigidbody>();
            //goalPoint.transform.position = new Vector3 (randomGoalx,t.terrainData.GetHeight(randomGoalx,randomGoalz),randomGoalz);

            Time.timeScale = 3;
            findTime = 0;
            bestTime = 500f;
        }
        protected virtual void Update(){
            if(count == 1){
                FindPlayer();
                var goalPoint = GameObject.Find("GoalPoint").GetComponent<Rigidbody>();
                count = 0;
            }
            //horizontalKey = horizontalInput.GetAxisRaw();
            //verticalKey = verticallInput.GetAxisRaw();
            //doSprint = sprintInput.GetButton();
            //doJump = jumpInput.GetButtonDown();
            new_key = BehaviorRecord();
            if(new_key != last_key){
                Debug.Log(stateRecord(new_key));
                sw.WriteLine(stateRecord(new_key));
                timer = 0;
            }
            last_key = new_key;
            timer += Time.deltaTime;
            /*if(currentController.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Actions.FreeClimb.ClimbWall")){
                    timer = 0;
                    tp_input.readState(action_list[++actionNum]);
                    Debug.Log("now action: "+action_list[actionNum]+", your position: "+currentPlayer.transform.position+", script position: "+script_position_list[actionNum]);
                    actionNum++;
            }*/
            if(timer > waitingTime){
                sw.WriteLine(stateRecord(new_key));
                if(actionNum != action_list.Count){
                    tp_input.readState(action_list[actionNum]);
                    Debug.Log("now action: "+action_list[actionNum]+", your position: "+currentPlayer.transform.position);
                    actionNum++;
                    timer = 0;
                }else{
                    tp_input.readState("Wait");
                    timer = 0;
                    Debug.Log("end coord: "+currentPlayer.transform.position);
                }
            }
            ///check the condition of reach at goal point
            if((int)currentPlayer.transform.position.x == (int)goalPoint.transform.position.x &&
                (int)currentPlayer.transform.position.z == (int)goalPoint.transform.position.z)
            {
                sw.WriteLine(currentController.transform.position);
                sw.WriteLine(startPos);
                sw.Flush();
                sw.Close();
                EndGame();
            }
            findTime += Time.deltaTime;
        }
        ///Read inputKey file
        public void reader(){
            string filepthR = "Assets/inputKeyTxt/input_"+caseNum;
            
            sr = new StreamReader(filepthR + ".log");
            bool endOfFile = false;
            while(!endOfFile){
                string data_String = sr.ReadLine();
                if(data_String == null){
                    endOfFile = true;
                    break;
                }
                if(data_String.Contains("coord")){
                    string[] position_data = data_String.Split('>');
                    script_position_list.Add(position_data[0]);
                }
                else if(data_String.Contains("action")){
                    string[] action_state = data_String.Split(',');
                    string[] action = action_state[1].Split(':');
                    action_list.Add(action[1]);
                }
                else{
                    data_String = sr.ReadLine();
                }
            }
        }
        ///End the game when reach at goal point, and display time for reached
        public void EndGame(){
            gameoverText.SetActive(true);
            if(bestTime == 500){
                bestTime = findTime;
                PlayerPrefs.SetFloat("BestTime",bestTime);
                recordText.text = "Best Record: " + (int) bestTime;
            }
            if(!currentController.isDead && bestTime != 500){
                Debug.Log(currentController.isDead);
                Debug.Log("animator"+currentController.animator.GetBool("isDead"));
                bestTime = PlayerPrefs.GetFloat("BestTime");
                if(findTime < bestTime){
                    bestTime = findTime;
                    PlayerPrefs.SetFloat("BestTime",bestTime);
                }
                recordText.text = "Best Record: " + (int) bestTime;
            }
            if(Input.GetKeyDown(KeyCode.R)){
                currentController.onDead.AddListener(OnCharacterDead);
                Invoke("ResetScene", respawnTimer);
            }
        }

        ///record keyInput when you play directily in Unity
        public String stateRecord(String key_input){
            Vector3 vel = new Vector3(0,0,0);
            float moveSpeed = 4f;
            Vector3 climb_dir = tp_input.cc.transform.position - tp_input.tpCamera.transform.position;
            float acting_time = 1f;
            float stamina_consume = 0f;
            
            
            int act_num = Array.IndexOf(actionNumber, key_input);

            if(currentController.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Actions.FreeClimb.ClimbWall")){
                acting_time = 1.3f;
                stamina_consume = acting_time * 10f;
                if(key_input.Contains("W")) vel = vel + new Vector3(climb_dir.x, 1f, climb_dir.z);
                if(key_input.Contains("S")) vel = vel - new Vector3(climb_dir.x, 1f, climb_dir.z);
            }
            else if(currentController.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Actions.Parachute.Parachute")){
                vel = vel - new Vector3(0f, 3f, 0f);
                acting_time = 1.3f;
                stamina_consume = acting_time * 5f;
                if(key_input.Contains("W")) vel = vel + new Vector3(moveSpeed, 0f, 0f);
                if(key_input.Contains("S")) vel = vel - new Vector3(moveSpeed, 0f, 0f);
                if(key_input.Contains("A")) vel = vel - new Vector3(0f, 0f, moveSpeed);
                if(key_input.Contains("D")) vel = vel + new Vector3(0f, 0f, moveSpeed);
            }
            else{
                
                if(key_input.Contains("s")){    moveSpeed = 6f; stamina_consume = 30f;}
                if(!String.Equals(key_input, "wait") && !String.Equals(key_input,"j")){
                    vel = rotateVel(new_key) * moveSpeed;
                }
                if(key_input.Contains("j")){    vel = vel + new Vector3(0f, 3.822f, 0f);  stamina_consume += 1f;}

                // currentController.transform.position
            }

            return act_num+", "+acting_time+", "+vel+", "+stamina_consume;
        }
        ///rotate Vector for recording directly keyInput at txt file
        public Vector3 rotateVel(String tmp_input){
            if(tmp_input.Contains("s")) tmp_input = tmp_input.Replace("s","");
            if(tmp_input.Contains("j")) tmp_input = tmp_input.Replace("j","");
            Vector3 vel = new Vector3 (0f,0f,1f);
            Quaternion velRotate = Quaternion.Euler(0f, angles[tmp_input], 0f);
            vel = velRotate * vel;
            return vel;
        }
        ///Get keyInput state from Unity player move
        public virtual String BehaviorRecord(){
            String KeyInput="";

            if(verticalKey == 1){
                KeyInput += "W";
            }
            else if(verticalKey == -1){
                KeyInput += "S";
            }
            if(horizontalKey == 1){
                KeyInput += "D";
            }
            else if(horizontalKey == -1){
                KeyInput += "A";
            }
            if(doJump == true){
                KeyInput += "j";
            }
            else if(doSprint == true){
                KeyInput += "s";
            }
            if(verticalKey == 0 && horizontalKey == 0 && doSprint == false && doJump == false){
                KeyInput += "wait";
            }
            
            return KeyInput;

            
        }
        protected virtual void OnCharacterDead(GameObject _gameObject)
        {
            Debug.Log("oncharacterdead");
            oldPlayer = _gameObject;

            if (playerPrefab != null)
            {
                StartCoroutine(RespawnRoutine());
            }
            else
            {
                if (displayInfoInFadeText && vHUDController.instance)
                {
                    vHUDController.instance.ShowText("Restarting Scene...");
                }

                Invoke("ResetScene", respawnTimer);
            }
        }

        protected virtual IEnumerator RespawnRoutine()
        {
            Debug.Log("RespawnRoutine");
            yield return new WaitForSeconds(respawnTimer);

            if (playerPrefab != null && spawnPoint != null)
            {
                if (oldPlayer != null && destroyBodyAfterDead)
                {
                    if (displayInfoInFadeText && vHUDController.instance)
                    {
                        vHUDController.instance.ShowText("Player destroyed: " + oldPlayer.name.Replace("(Clone)", "").Replace("Instance", ""));
                    }

                    Destroy(oldPlayer);
                }
                else
                {
                    if (displayInfoInFadeText && vHUDController.instance)
                    {
                        vHUDController.instance.ShowText("Remove Player Components: " + oldPlayer.name.Replace("(Clone)", "").Replace("Instance", ""));
                    }

                    DestroyPlayerComponents(oldPlayer);
                }

                yield return new WaitForEndOfFrame();

                currentPlayer = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
                currentController = currentPlayer.GetComponent<vThirdPersonController>();
                currentController.onDead.AddListener(OnCharacterDead);

                if (displayInfoInFadeText && vHUDController.instance)
                {
                    vHUDController.instance.ShowText("Respawn player: " + currentPlayer.name.Replace("(Clone)", ""));
                }

                OnReloadGame.Invoke();
                onSpawn.Invoke();
            }
        }

        protected virtual void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            if (currentController.currentHealth > 0)
            {
                if (displayInfoInFadeText && vHUDController.instance)
                {
                    vHUDController.instance.ShowText("Load Scene: " + scene.name);
                }

                return;
            }
            if (displayInfoInFadeText && vHUDController.instance)
            {
                vHUDController.instance.ShowText("Reload Scene");
            }

            OnReloadGame.Invoke();
            FindPlayer();

        }

        protected virtual void FindPlayer()
        {
            var player = GameObject.FindObjectOfType<vThirdPersonController>();
            if (player)
            {
                currentPlayer = player.gameObject;
                currentController = player;
                player.onDead.AddListener(OnCharacterDead);
                if (displayInfoInFadeText && vHUDController.instance)
                {
                    vHUDController.instance.ShowText("Found player: " + currentPlayer.name.Replace("(Clone)", "").Replace("Instance", ""));
                }
            }
            else if (currentPlayer == null && playerPrefab != null && spawnPoint != null)
            {
                SpawnAtPoint(spawnPoint);
            }
        }

        protected virtual void DestroyPlayerComponents(GameObject target)
        {
            if (!target)
            {
                return;
            }

            var comps = target.GetComponentsInChildren<MonoBehaviour>();
            for (int i = 0; i < comps.Length; i++)
            {
                Destroy(comps[i]);
            }
            var coll = target.GetComponent<Collider>();
            if (coll != null)
            {
                Destroy(coll);
            }

            var rigidbody = target.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                Destroy(rigidbody);
            }

            var animator = target.GetComponent<Animator>();
            if (animator != null)
            {
                Destroy(animator);
            }
        }

        /// <summary>
        /// Set a custom spawn point (or use it as checkpoint to your level) 
        /// </summary>
        /// <param name="newSpawnPoint"> new point to spawn</param>
        public virtual void SetSpawnSpoint(Transform newSpawnPoint)
        {
            spawnPoint = newSpawnPoint;
        }

        /// <summary>
        /// Spawn New Player at a specific point
        /// </summary>
        /// <param name="targetPoint"> Point to spawn player</param>
        public virtual void SpawnAtPoint(Transform targetPoint)
        {
            if (playerPrefab != null)
            {
                if (oldPlayer != null && destroyBodyAfterDead)
                {
                    if (displayInfoInFadeText && vHUDController.instance)
                    {
                        vHUDController.instance.ShowText("Player destroyed: " + oldPlayer.name.Replace("(Clone)", "").Replace("Instance", ""));
                    }

                    Destroy(oldPlayer);
                }

                else if (oldPlayer != null)
                {
                    if (displayInfoInFadeText && vHUDController.instance)
                    {
                        vHUDController.instance.ShowText("Remove Player Components: " + oldPlayer.name.Replace("(Clone)", "").Replace("Instance", ""));
                    }

                    DestroyPlayerComponents(oldPlayer);
                }

                currentPlayer = Instantiate(playerPrefab, targetPoint.position, targetPoint.rotation);
                currentController = currentPlayer.GetComponent<vThirdPersonController>();
                currentController.onDead.AddListener(OnCharacterDead);
                OnReloadGame.Invoke();

                if (displayInfoInFadeText && vHUDController.instance)
                {
                    vHUDController.instance.ShowText("Spawn player: " + currentPlayer.name.Replace("(Clone)", ""));
                }
            }
        }

        /// <summary>
        /// Reload  current Scene and current Player
        /// </summary>
        public virtual void ResetScene()
        {
            Debug.Log("ResetScene");
            if (oldPlayer)
            {
                DestroyPlayerComponents(oldPlayer);
            }
            ///reset time state for retry on same map and environment
            var scene = SceneManager.GetActiveScene();
            findTime=0;
            SceneManager.LoadScene(scene.name);
            count = 1;
            
            if (oldPlayer && destroyBodyAfterDead)
            {
                Destroy(oldPlayer);
            }
        }

    }
}