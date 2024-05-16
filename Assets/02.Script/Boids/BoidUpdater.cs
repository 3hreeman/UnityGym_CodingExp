using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Serialization;

public class BoidUpdater : MonoBehaviour {

    public Boids main;
    
    public int maxNeighbourCount = 10;
    public float DetectDistance = 6;
    
    private FindNeighborJob[] findNeighborJobList;
    private JobHandle[] findNeighborJobHandleList;
    
    private BoidPositionUpdateJob posUpdateJob;
    private JobHandle posUpdateJobHandle;
    
    private TransformAccessArray transformAccessArray;
    
    private NativeArray<BoidUnit.MoveData> _moveDatas;
    // private NativeArray<Vector3> _positions;
    public void Init(Boids main) {
        this.main = main;
        findNeighborJobList = new FindNeighborJob[main.boidCount];
        findNeighborJobHandleList = new JobHandle[main.boidCount];
        transformAccessArray = new TransformAccessArray(main.boidCount);
        foreach(var boid in main.boidList) {
            AddBoid(boid);
        }
        _moveDatas = new NativeArray<BoidUnit.MoveData>(main.boidList.Select(e=>e.moveData).ToArray(), Allocator.Persistent);
        // _positions = new NativeArray<Vector3>(main.boidList.Select(e=>e.transform.position).ToArray(), Allocator.Persistent);
    }
    
    public void AddBoid(BoidUnit unit) {
        transformAccessArray.Add(unit.transform);
    }
    
    public void RemoveBoid(int idx) {
        //TODO need specified enemy remove logic 
        transformAccessArray.RemoveAtSwapBack(idx);
    }

    private void BoidUpdate() {
        for(int i=0; i<main.boidList.Count; i++) {
            var curBoid = main.boidList[i];
            _moveDatas[i] = curBoid.moveData;
            var newArray = new NativeArray<Vector3>(main.boidList.Select(e => e.transform.position).ToArray(),
                Allocator.Persistent);
            findNeighborJobList[i] = new FindNeighborJob() {
                positions = newArray,
                decectRange = DetectDistance,
                maxNeighbourCount = maxNeighbourCount,
                neighbors = new NativeArray<int>(maxNeighbourCount, Allocator.TempJob)
            };
            findNeighborJobHandleList[i] = findNeighborJobList[i].Schedule(newArray.Length, 64);
            curBoid.UpdateNeighbor(findNeighborJobList[i].neighbors);
            curBoid.UpdateForJob();
        }
        
        posUpdateJob = new BoidPositionUpdateJob() {
            targetData = _moveDatas,
            jobDeltaTime = Time.deltaTime
        };
        
        posUpdateJobHandle = posUpdateJob.Schedule(transformAccessArray);
    }

    private void OnDestroy() {
        posUpdateJobHandle.Complete();
        foreach (var findNeighborJobHandle in findNeighborJobHandleList) {
            findNeighborJobHandle.Complete();
        }
        _moveDatas.Dispose();
        posUpdateJob.targetData.Dispose();
        transformAccessArray.Dispose();
    }

    public void UpdateJob() {
        BoidUpdate();
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
    
    [BurstCompile]
    struct FindNeighborJob : IJobParallelFor {
        public NativeArray<Vector3> positions;
        public float decectRange;
        public int maxNeighbourCount;
        public NativeArray<int> neighbors;
        
        public void Execute(int index) {
            var pos = positions[index];
            //set default value -1 to neighbors;
            for (int i = 0; i < neighbors.Length; i++) {
                neighbors[i] = -1;
            }
            int curCount = 0;
            for (int i = 0; i < positions.Length; i++) {
                if (i == index) continue;
                if (Vector3.Distance(pos, positions[i]) < decectRange) {
                    neighbors[curCount] = i;
                    curCount++;
                }
                if(neighbors.Length >= maxNeighbourCount) break;
            }
        }
    }
}

