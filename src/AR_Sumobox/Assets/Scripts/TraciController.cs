﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Traci = CodingConnected.TraCI.NET;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

/// <summary>
/// Traci Controller class manages a running simulation by communicating with a Sumo process. 
/// </summary>
public class TraciController : MonoBehaviour
{
    /// <summary>
    /// The Car main Game Object
    /// </summary>
    public GameObject Cars_GO;
    /// <summary>
    /// The simulation speed.
    /// </summary>
    public float speed = 2.0f;
    /// <summary>
    /// The Traci client.
    /// </summary>
    public Traci.TraCIClient Client;
    /// <summary>
    /// The hostname of the computer for remote connections.
    /// </summary>
    public String HostName;
    /// <summary>
    /// The post of the computer for remote connections.
    /// </summary>
    public int Port;
    /// <summary>
    /// The current simulation config file.
    /// </summary>
    public String ConfigFile;
    /// <summary>
    /// Simulation elapsed time.
    /// </summary>
    private float Elapsedtime;

    /// <summary>
    /// Called when the scene is first rendered
    /// </summary>
    void Start()
    {
        Cars_GO = GameObject.Find("Cars");
        
    }
    
    /// <summary>
    /// This connects to sumo asynchronously
    /// </summary>
    /// <returns>A task to await</returns>
    async Task <Traci.TraCIClient> ConnectToSumo()
    {
        try
        {
            Client = new Traci.TraCIClient();
            Client.VehicleSubscription += OnVehicleUpdate;
            string tmp = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..\\Sumo\\bin\\"));
            Process p = new Process();
            ProcessStartInfo si = new ProcessStartInfo()
            {
                WorkingDirectory = "C:\\Sumo\\bin\\",
                FileName = "sumo.exe",
                Arguments = " --remote-port " + Port.ToString() + " --configuration-file " + ConfigFile
            };
            p.StartInfo = si;
            p.Start();
            Thread.Sleep(400);
            //Connect to sumo running on specified port
            await Client.ConnectAsync(HostName, Port);
            Subscribe();
            Client.Control.SimStep();
            return Client;
        }
        catch(Exception e)
        {
            UnityEngine.Debug.LogWarning(e.Message);
            return null;
        }
    }

    /// <summary>
    /// Removes the construction zone attribute for a defined lane in the given road, and updates the simulation in SUMO.
    /// </summary>
    /// <param name="Road">The gameobject to whom we will update the specified lane</param>
    /// <param name="LaneId">The lane Id as specified in the SUMO network file</param>
    public void RemoveWorkZoneOnLane(GameObject Road, String LaneId)
    {
        Road FoundRoad = Road.GetComponent<Edge>().RoadList.Find(found => found.Name == Road.name);
        Lane FoundLane = FoundRoad.Lanes.Find(L => L.Id == LaneId);
        if (FoundLane.ConstructionZone)
        {
            FoundLane.Speed = FoundLane.DefaultSpeed;
            FoundLane.ConstructionZone = false;
            Client.Edge.SetMaxSpeed(FoundRoad.Id, (double)Int32.Parse(FoundLane.DefaultSpeed));
        }
        else
        {
            UnityEngine.Debug.LogWarning("Lane: " + LaneId + " Is not a construction zone");
        }
    }

    /// <summary>
    /// Removes the construction zone attribute from every lane in the road, and updates the simulation accordingly in SUMO.
    /// </summary>
    /// <param name="Road">The Road GameObject with an Edge component of roads to update </param>
    public void RemoveWorkZoneEntireRoad(GameObject Road)
    {
        Road FoundRoad = Road.GetComponent<Edge>().RoadList.Find(found => found.Name == Road.name);
        FoundRoad.Lanes.ForEach(FoundLane => {
            if (!FoundLane.ConstructionZone)
            {
                FoundLane.Speed = FoundLane.DefaultSpeed;
                FoundLane.ConstructionZone = false;
                Client.Edge.SetMaxSpeed(FoundRoad.Id, (double)Int32.Parse(FoundLane.DefaultSpeed));
            }
        });
    }

    /// <summary>
    /// Sets the construction zone attribute for every lane in the road, and updates the simulation accordingly in SUMO.
    /// </summary>
    /// <param name="Road">The Road GameObject to update the road</param>
    public void SetWorkZoneEntireRoad(GameObject Road)
    {
        Road FoundRoad = Road.GetComponent<Edge>().RoadList.Find(found => found.Name == Road.name);
        FoundRoad.Lanes.ForEach(FoundLane => {
            if (!FoundLane.ConstructionZone)
            {
                int Speed = Int32.Parse(FoundLane.Speed);
                //Gets the smallest, 0.75 * the speed, or 45 mph
                Speed = (Speed * 3 / 4) > 45 ? (Speed * 3 / 4) : 45;
                FoundLane.Speed = Speed.ToString();
                FoundLane.ConstructionZone = true;
                Client.Edge.SetMaxSpeed(FoundRoad.Id, (double)Speed);
            }
        });
    }

    /// <summary>
    /// Sets the construction zone attribute for a defined lane in the given road, and updates the simulation in SUMO.
    /// </summary>
    /// <param name="Road">The gameobject to whom we will update the specified lane</param>
    /// <param name="LaneId">The lane Id as specified in the SUMO network file</param>
    public void SetWorkZoneOneLane(GameObject Road, String LaneId)
    {
        Road FoundRoad = Road.GetComponent<Edge>().RoadList.Find(found => found.Name == Road.name);
        Lane FoundLane = FoundRoad.Lanes.Find(L => L.Id == LaneId);
        if (!FoundLane.ConstructionZone)
        {
            int Speed = Int32.Parse(FoundLane.Speed);
            //Gets the smallest, 0.75 * the speed, or 45 mph
            Speed = (Speed * 3 / 4) > 45 ? (Speed * 3 / 4) : 45;
            FoundLane.Speed = Speed.ToString();
            FoundLane.ConstructionZone = true;
            Client.Edge.SetMaxSpeed(FoundRoad.Id, (double)Speed);
        }
        else
        {
            UnityEngine.Debug.LogWarning("Lane: " + LaneId + " Is already a construction zone");
        }
    }

    /// <summary>
    /// Subscribes to all vehicles in the simulation
    /// </summary>
    public void Subscribe()
    {
        List<byte> carInfo = new List<byte> { Traci.TraCIConstants.POSITION_3D };

        // Get all the car ids we need to keep track of. 
        Traci.TraCIResponse<List<String>> CarIds = Client.Vehicle.GetIdList();

        // Subscribe to all cars from 0 to 2147483647, and get their 3d position data
        CarIds.Content.ForEach(car => Client.Vehicle.Subscribe(car, 0, 2147483647, carInfo));
    }

    /// <summary>
    /// Event handler to handle a car update event
    /// </summary>
    /// <param name="sender">The client</param>
    /// <param name="e">The event args</param>
    public void OnVehicleUpdate(object sender, Traci.Types.SubscriptionEventArgs e)
    {
        GameObject Car_GO = GameObject.Find(e.ObjecId);
        if (Car_GO != null)
        {
            Cars_GO.transform.position = (Vector3)e.Responses.ToArray()[0];
        }
        else
        {
            SphereCollider car = Cars_GO.AddComponent(typeof(SphereCollider)) as SphereCollider;
            car.tag = e.ObjecId;
            Traci.Types.Position3D pos = (Traci.Types.Position3D)e.Responses.ToArray()[0];
            car.transform.position = new Vector3((float)pos.X, 0, (float)pos.Y);
        }
    }

    /// <summary>
    /// Update is called once per frame
    /// If the client is defined, it will attempt to get a list of vehicles who are currently active and set their positions/create them accordingly. 
    /// </summary>
    void Update()
    {
        // Get all the car ids we need to keep track of. 
        if (Client != null)
        {
            
            Traci.TraCIResponse<List<String>> CarIds = Client.Vehicle.GetIdList();

            CarIds.Content.ForEach(carId => {
                Traci.Types.Position3D pos = Client.Vehicle.GetPosition3D(carId).Content;
                Transform CarTransform = Cars_GO.transform.Find(carId);
                if (CarTransform != null)
                {
                    CarTransform.position = new Vector3((float)pos.X, 0, (float)pos.Y);
                }
                else
                {
                    GameObject car = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    car.name = carId;
                    car.transform.parent = Cars_GO.transform;
                    car.transform.position = new Vector3((float)pos.X, 1, (float)pos.Y);
                }
            });
            Elapsedtime += Time.deltaTime;
            if(Elapsedtime > 1)
            {
                Client.Control.SimStep();
                Elapsedtime = 0;
            }
        }
        
    }
}
