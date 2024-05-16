using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

public class BoidUpdater : MonoBehaviour {

    public Boids main;
    private BoidPositionUpdateJob posUpdateJob;
    private JobHandle posUpdateJobHandle;
    
    private TransformAccessArray transformAccessArray;
    
    
    public void Init(Boids main) {
        this.main = main;
        transformAccessArray = new TransformAccessArray(main.boidCount);
        foreach(var boid in main.boidList) {
            AddBoid(boid);
        }
    }
    
    public void AddBoid(BoidUnit unit) {
        transformAccessArray.Add(unit.transform);
    }
    
    public void RemoveBoid(int idx) {
        //TODO need specified enemy remove logic 
        transformAccessArray.RemoveAtSwapBack(idx);
    }

    private void BoidUpdate(List<BoidUnit> boidList) {
        posUpdateJob = new BoidPositionUpdateJob() {
            targetData = new NativeArray<BoidUnit.MoveData>(boidList.Select(e=>e.moveData).ToArray(), Allocator.TempJob),
            jobDeltaTime = Time.deltaTime
        };
        
        posUpdateJobHandle = posUpdateJob.Schedule(transformAccessArray);
    }
    
    public void UpdateJob() {
        BoidUpdate(main.boidList);
    }
    
    
    [BurstCompile]
    struct BoidPositionUpdateJob : IJobParallelForTransform {
        public NativeArray<BoidUnit.MoveData> targetData; 
        public float jobDeltaTime;
        
        public void Execute(int index, TransformAccess transform) {
            var data = targetData[index];
            
            transform.rotation = Quaternion.LookRotation(targetData[index].targetVec);
            transform.position += targetData[index].targetVec * (targetData[index].speed + targetData[index].additionalSpeed) * jobDeltaTime;
        }
    }
}

