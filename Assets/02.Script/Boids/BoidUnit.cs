using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

public class BoidUnit : MonoBehaviour {
    public struct MoveData {
        public float speed;
        public float additionalSpeed;
        public Vector3 targetVec;
        public Vector3 cohesionVec;
        public Vector3 alignmentVec;
        public Vector3 separationVec;

        public Vector3 boundsVec;
        public Vector3 obstacleVec;
        public Vector3 egoVec;
        public Vector3 egoVector;
    }

    #region Variables & Initializer

    [Header("Info")] Boids myBoids;
    List<BoidUnit> neighbours = new List<BoidUnit>();

    bool isEnemy;

    MeshRenderer myMeshRenderer;
    TrailRenderer myTrailRenderer;
    [SerializeField] private Color myColor;

    [Header("Neighbour")] [SerializeField] float obstacleDistance;
    [SerializeField] float FOVAngle = 120;
    [SerializeField] float maxNeighbourCount = 50;
    [SerializeField] float neighbourDistance = 10;

    [Header("ETC")] [SerializeField] LayerMask boidUnitLayer;
    [SerializeField] LayerMask obstacleLayer;

    Coroutine findNeighbourCoroutine;
    Coroutine calculateEgoVectorCoroutine;

    public MoveData moveData;

    public void InitializeUnit(Boids _boids, float _speed, int _myIndex) {
        moveData = new MoveData();
        myBoids = _boids;
        moveData.speed = _speed;
        moveData.additionalSpeed = 0;

        myTrailRenderer = GetComponentInChildren<TrailRenderer>();
        myMeshRenderer = GetComponentInChildren<MeshRenderer>();

        // set Color
        if (myBoids.randomColor) {
            myColor = new Color(Random.value, Random.value, Random.value);
            myMeshRenderer.material.color = myColor;
        }
        else if (myBoids.blackAndWhite) {
            float myIndexFloat = _myIndex;
            myColor = new Color(myIndexFloat / myBoids.boidCount, myIndexFloat / myBoids.boidCount,
                myIndexFloat / myBoids.boidCount, 1f);
        }
        else {
            myColor = myMeshRenderer.material.color;
        }

        // is Enemy?
        if (Random.Range(0, 1f) < myBoids.enemyPercentage) {
            myColor = new Color(1, 0, 0);
            isEnemy = true;
            transform.gameObject.layer = LayerMask.NameToLayer("Obstacle");
        }

        if (Boids.instance.UseUniTask) {
            FindNeighbour().Forget();
            CalcEgoVector().Forget();
        }
        else {
            findNeighbourCoroutine = StartCoroutine(FindNeighbourCoroutine());
            calculateEgoVectorCoroutine = StartCoroutine(CalculateEgoVectorCoroutine());
        }
    }

    public async UniTask FindNeighbour() {
        while (true) {
            neighbours.Clear();

            Collider[] colls = Physics.OverlapSphere(transform.position, neighbourDistance, boidUnitLayer);
            for (int i = 0; i < colls.Length; i++) {
                if (Vector3.Angle(transform.forward, colls[i].transform.position - transform.position) <= FOVAngle) {
                    neighbours.Add(colls[i].GetComponent<BoidUnit>());
                }

                if (i > maxNeighbourCount) {
                    break;
                }
            }

            await UniTask.Delay(TimeSpan.FromSeconds(Random.Range(0.5f, 2f)));
        }
    }

    public async UniTask CalcEgoVector() {
        while (true) {
            moveData.speed = Random.Range(myBoids.speedRange.x, myBoids.speedRange.y);
            moveData.egoVector = Random.insideUnitSphere;
            await UniTask.Delay(TimeSpan.FromSeconds(Random.Range(1f, 3f)));
        }
    }

    #endregion

    void Update() {
        if (Boids.instance.UseJob) {
            UpdateForJob();
        }
        else {
            UpdateForSelf();
        }
    }

    public void UpdateForSelf() {
        if (moveData.additionalSpeed > 0)
            moveData.additionalSpeed -= Time.deltaTime;

        // Calculate all the vectors we need
        moveData.cohesionVec = CalculateCohesionVector() * myBoids.cohesionWeight;
        moveData.alignmentVec = CalculateAlignmentVector() * myBoids.alignmentWeight;
        moveData.separationVec = CalculateSeparationVector() * myBoids.separationWeight;
        // �߰����� ����
        moveData.boundsVec = CalculateBoundsVector() * myBoids.boundsWeight;
        moveData.obstacleVec = CalculateObstacleVector() * myBoids.obstacleWeight;
        moveData.egoVec = moveData.egoVector * myBoids.egoWeight;

        if (isEnemy) {
            moveData.targetVec = moveData.boundsVec + moveData.obstacleVec + moveData.egoVector;
        }
        else {
            moveData.targetVec = moveData.cohesionVec + moveData.alignmentVec + moveData.separationVec +
                                 moveData.boundsVec + moveData.obstacleVec + moveData.egoVec;
        }

        // Steer and Move
        moveData.targetVec = Vector3.Lerp(this.transform.forward, moveData.targetVec, Time.deltaTime);
        moveData.targetVec = moveData.targetVec.normalized;
        if (moveData.targetVec == Vector3.zero)
            moveData.targetVec = moveData.egoVector;

        this.transform.rotation = Quaternion.LookRotation(moveData.targetVec);
        this.transform.position += moveData.targetVec * (moveData.speed + moveData.additionalSpeed) * Time.deltaTime;


        // Color Lerp
        if (myBoids.protectiveColor && !isEnemy && neighbours.Count > 0) {
            Vector3 colorSum = new Vector3(myColor.r, myColor.g, myColor.b);
            for (int i = 0; i < neighbours.Count; i++) {
                Color tmpColor = neighbours[i].myColor;
                colorSum += new Vector3(tmpColor.r, tmpColor.g, tmpColor.b);
            }

            myMeshRenderer.material.color = Color.Lerp(myMeshRenderer.material.color,
                new Color(colorSum.x / neighbours.Count, colorSum.y / neighbours.Count, colorSum.z / neighbours.Count,
                    1f), Time.deltaTime);
        }
        else {
            myMeshRenderer.material.color = Color.Lerp(myMeshRenderer.material.color, myColor, Time.deltaTime);
        }
    }

    public void UpdateForJob() {
        if (moveData.additionalSpeed > 0)
            moveData.additionalSpeed -= Time.deltaTime;

        // Calculate all the vectors we need
        moveData.cohesionVec = CalculateCohesionVector() * myBoids.cohesionWeight;
        moveData.alignmentVec = CalculateAlignmentVector() * myBoids.alignmentWeight;
        moveData.separationVec = CalculateSeparationVector() * myBoids.separationWeight;
        // �߰����� ����
        moveData.boundsVec = CalculateBoundsVector() * myBoids.boundsWeight;
        moveData.obstacleVec = CalculateObstacleVector() * myBoids.obstacleWeight;
        moveData.egoVec = moveData.egoVector * myBoids.egoWeight;

        if (isEnemy) {
            moveData.targetVec = moveData.boundsVec + moveData.obstacleVec + moveData.egoVector;
        }
        else {
            moveData.targetVec = moveData.cohesionVec + moveData.alignmentVec + moveData.separationVec +
                                 moveData.boundsVec + moveData.obstacleVec + moveData.egoVec;
        }

        // Steer and Move
        moveData.targetVec = Vector3.Lerp(this.transform.forward, moveData.targetVec, Time.deltaTime);
        moveData.targetVec = moveData.targetVec.normalized;
        if (moveData.targetVec == Vector3.zero)
            moveData.targetVec = moveData.egoVector;

        // Color Lerp
        if (myBoids.protectiveColor && !isEnemy && neighbours.Count > 0) {
            Vector3 colorSum = new Vector3(myColor.r, myColor.g, myColor.b);
            for (int i = 0; i < neighbours.Count; i++) {
                Color tmpColor = neighbours[i].myColor;
                colorSum += new Vector3(tmpColor.r, tmpColor.g, tmpColor.b);
            }

            myMeshRenderer.material.color = Color.Lerp(myMeshRenderer.material.color,
                new Color(colorSum.x / neighbours.Count, colorSum.y / neighbours.Count, colorSum.z / neighbours.Count,
                    1f), Time.deltaTime);
        }
        else {
            myMeshRenderer.material.color = Color.Lerp(myMeshRenderer.material.color, myColor, Time.deltaTime);
        }
    }


    #region Calculate Vectors

    IEnumerator CalculateEgoVectorCoroutine() {
        moveData.speed = Random.Range(myBoids.speedRange.x, myBoids.speedRange.y);
        moveData.egoVector = Random.insideUnitSphere;
        yield return new WaitForSeconds(Random.Range(1, 3f));
        calculateEgoVectorCoroutine = StartCoroutine("CalculateEgoVectorCoroutine");
    }

    IEnumerator FindNeighbourCoroutine() {
        neighbours.Clear();

        Collider[] colls = Physics.OverlapSphere(transform.position, neighbourDistance, boidUnitLayer);
        for (int i = 0; i < colls.Length; i++) {
            if (Vector3.Angle(transform.forward, colls[i].transform.position - transform.position) <= FOVAngle) {
                neighbours.Add(colls[i].GetComponent<BoidUnit>());
            }

            if (i > maxNeighbourCount) {
                break;
            }
        }

        yield return new WaitForSeconds(Random.Range(0.5f, 2f));
        findNeighbourCoroutine = StartCoroutine("FindNeighbourCoroutine");
    }

    private Vector3 CalculateCohesionVector() {
        Vector3 cohesionVec = Vector3.zero;
        if (neighbours.Count > 0) {
            // �̿� unit���� ��ġ ���ϱ�
            for (int i = 0; i < neighbours.Count; i++) {
                cohesionVec += neighbours[i].transform.position;
            }
        }
        else {
            // �̿��� ������ vector3.zero ��ȯ
            return cohesionVec;
        }

        // �߽� ��ġ���� ���� ã��
        cohesionVec /= neighbours.Count;
        cohesionVec -= transform.position;
        cohesionVec.Normalize();
        return cohesionVec;
    }

    private Vector3 CalculateAlignmentVector() {
        Vector3 alignmentVec = transform.forward;
        if (neighbours.Count > 0) {
            // �̿����� ���ϴ� ������ ��� �������� �̵�
            for (int i = 0; i < neighbours.Count; i++) {
                alignmentVec += neighbours[i].transform.forward;
            }
        }
        else {
            // �̿��� ������ �׳� forward�� �̵�
            return alignmentVec;
        }

        alignmentVec /= neighbours.Count;
        alignmentVec.Normalize();
        return alignmentVec;
    }

    private Vector3 CalculateSeparationVector() {
        Vector3 separationVec = Vector3.zero;
        if (neighbours.Count > 0) {
            // �̿����� ���ϴ� �������� �̵�
            for (int i = 0; i < neighbours.Count; i++) {
                separationVec += (transform.position - neighbours[i].transform.position);
            }
        }
        else {
            // �̿��� ������ vector.zero ��ȯ
            return separationVec;
        }

        separationVec /= neighbours.Count;
        separationVec.Normalize();
        return separationVec;
    }

    private Vector3 CalculateBoundsVector() {
        Vector3 offsetToCenter = myBoids.transform.position - transform.position;
        return offsetToCenter.magnitude >= myBoids.spawnRange ? offsetToCenter.normalized : Vector3.zero;
    }

    private Vector3 CalculateObstacleVector() {
        Vector3 obstacleVec = Vector3.zero;
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, obstacleDistance, obstacleLayer)) {
            Debug.DrawLine(transform.position, hit.point, Color.black);
            obstacleVec = hit.normal;
            moveData.additionalSpeed = 10;
        }

        return obstacleVec;
    }

    #endregion


    public void DrawVectorGizmo(int _depth) {
        for (int i = 0; i < neighbours.Count; i++) {
            if (_depth + 1 < myBoids.GizmoColors.Length - 1)
                neighbours[i].DrawVectorGizmo(_depth + 1);

            Debug.DrawLine(this.transform.position, neighbours[i].transform.position, myBoids.GizmoColors[_depth + 1]);
            Debug.DrawLine(this.transform.position, this.transform.position + moveData.targetVec,
                myBoids.GizmoColors[0]);
        }
    }
}