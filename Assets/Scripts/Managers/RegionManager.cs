using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RegionManager : MonoBehaviour
{
    private List<ChargedObject> chargedObjects;
    private List<MovingChargedObject> movingChargedObjects;
    private bool isInitialized = false;
    private bool hasAppliedStartVelocity = false;

    void Start()
    {
        foreach (MovingChargedObject mChargedObj in FindObjectsOfType<MovingChargedObject>())
            mChargedObj.UpdateAppearance();
    }

    void Update()
    {
        if (!isInitialized && AllChargedObjectsAreGenerated())
        {
            FindChargedObjects();
            isInitialized = true;
        }

        if (!hasAppliedStartVelocity)
            ApplyStartVelocities();
    }

    private List<ChargedObject> GetChargedObjects()
    {
        if (chargedObjects == null)
            chargedObjects = new List<ChargedObject>();
        return chargedObjects;
    }

    private List<MovingChargedObject> GetMovingChargedObjects()
    {
        if (movingChargedObjects == null)
            movingChargedObjects = new List<MovingChargedObject>();
        return movingChargedObjects;
    }

    private void ApplyStartVelocities()
    {
        hasAppliedStartVelocity = true;
        foreach (MovingChargedObject mChargedObj in GetMovingChargedObjects())
            mChargedObj.ApplyStartVelocity();
    }

    public void AddChargedObject(ChargedObject chargedObj)
    {
        GetChargedObjects().Add(chargedObj);
        MovingChargedObject mChargedObj = chargedObj.gameObject.GetComponent<MovingChargedObject>();
        if (mChargedObj != null)
        {
            GetMovingChargedObjects().Add(mChargedObj);
            if (hasAppliedStartVelocity)
                mChargedObj.ApplyStartVelocity();
            StartCoroutine(Cycle(mChargedObj));
        }
    }

    public static RegionManager GetMyRegionManager(GameObject childObject)
    {
        RegionManager regionManager = null;
        Transform transformParent = childObject.transform.parent;
        while (regionManager == null && transformParent != null)
        {
            if (transformParent.gameObject.GetComponent<RegionManager>() != null)
            {
                regionManager = transformParent.GetComponent<RegionManager>();
                break;
            }
            transformParent = transformParent.parent;
        }

        return regionManager;
    }

    private bool AllChargedObjectsAreGenerated()
    {
        return true;
    }

    public void DestroyChargedObject(ChargedObject chargedObj)
    {
        MovingChargedObject mChargedObj = chargedObj.gameObject.GetComponent<MovingChargedObject>();
        if (mChargedObj != null)
        {
            if (GetMovingChargedObjects().Contains(mChargedObj))
                GetMovingChargedObjects().Remove(mChargedObj);
            else
                Debug.LogError("DestroyChargedObject called but no objects found");
        }
        GetChargedObjects().Remove(chargedObj);

        Destroy(chargedObj.gameObject);
        Destroy(chargedObj);
        Destroy(mChargedObj);
    }

    private void FindChargedObjects()
    {
        foreach (GameObject go in ParentChildFunctions.GetAllChildren(gameObject, false))
        {
            ChargedObject chargedObj = go.GetComponent<ChargedObject>();
            if (chargedObj != null)
                AddChargedObject(chargedObj);
        }
    }

    private IEnumerator RecalculateMagnetInterval()
    {
        while (true)
        {
            if (Time.smoothDeltaTime > 0)
            {
                float ratio = Time.smoothDeltaTime * GameSettings.targetFPS;
                float newInterval = GameSettings.magnetInterval * ratio;
                Debug.Log("smooth: " + Time.smoothDeltaTime + " old: " + GameSettings.magnetInterval + " new:" + newInterval + " ratio:" + ratio);
                newInterval = Mathf.Clamp(newInterval, GameSettings.minimumMagnetInterval, 1);
                GameSettings.magnetInterval = newInterval;
            }
            yield return new WaitForSeconds(1);
        }

    }

    private IEnumerator Cycle(MovingChargedObject mChargedObj)
    {
        bool isFirst = true;
        while (true)
        {
            if (isFirst)
            {
                isFirst = false;
                yield return new WaitForSeconds(Random.Range(0, GameSettings.magnetInterval));
            }

            if (mChargedObj == null)
            {
                break;
            }
            else
            {
                    ApplyMagneticForce(mChargedObj);
                yield return new WaitForSeconds(GameSettings.magnetInterval);
            }
        }
    }

    private void ApplyMagneticForce(MovingChargedObject mChargedObj)
    {
        Vector3 newForce = new Vector3(0, 0, 0);

        foreach (ChargedObject chargedObj in GetChargedObjects())
        {
            if (chargedObj == null)
            {

                string stuff = "";
                foreach (ChargedObject co in GetChargedObjects())
                    if (co != null)
                        stuff += " '" + co.gameObject + "'";
                    else
                        stuff += " null";
                Debug.Log("null thingy weird! " + GetChargedObjects().Count + "   " + stuff);
            }

            if (mChargedObj.GetChargedObject() == chargedObj || chargedObj.ignoreOtherMovingChargedObjects)
                continue;

            float distance = Vector3.Distance(mChargedObj.transform.position, chargedObj.gameObject.transform.position);
            float force = 1000 * mChargedObj.GetCharge() * chargedObj.charge / Mathf.Pow(distance, 2);
            Vector3 direction;

            direction = mChargedObj.transform.position - chargedObj.transform.position;
            direction.Normalize();

            newForce += force * direction * GameSettings.magnetInterval;
        }

        //if two charged particles occupy the same space, the newForce is (NaN,NaN,NaN) and AddForce throws an error
        if (float.IsNaN(newForce.x))
            newForce = Vector3.zero;

        mChargedObj.AddForce(newForce);
    }

}