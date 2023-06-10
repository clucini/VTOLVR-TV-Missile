using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Harmony;
using TV_Missile.TVMissile;

namespace TV_Missile
{
    public class Main : VTOLMOD
    {
        public static GameObject MFDGameObject;
        // This method is run once, when the Mod Loader is done initialising this game object
        public override void ModLoaded()
        {
            HarmonyInstance harmonyInstance = HarmonyInstance.Create("clucini.TVMissile");
            harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());


            Debug.Log("Loading MFD Object");
            MFDGameObject = FileLoader.LoadMFDGameobject($"{ModFolder}/tvmfd.mfd");

            VTOLAPI.SceneLoaded += SceneLoaded;
            base.ModLoaded();

            Log("Testing");
        }

        //This method is called every frame by Unity. Here you'll probably put most of your code
        void Update()
        {
        }

        //This method is like update but it's framerate independent. This means it gets called at a set time interval instead of every frame. This is useful for physics calculations
        void FixedUpdate()
        {

        }

        //This function is called every time a scene is loaded. this behaviour is defined in Awake().
        private void SceneLoaded(VTOLScenes scene)
        {
            //If you want something to happen in only one (or more) scenes, this is where you define it.

            //For example, lets say you're making a mod which only does something in the ready room and the loading scene. This is how your code could look:
            switch (scene)
            {
                case VTOLScenes.ReadyRoom:
                    //Add your ready room code here
                    break;
                case VTOLScenes.LoadingScene:
                    //Add your loading scene code here
                    break;
            }
        }
    }
}