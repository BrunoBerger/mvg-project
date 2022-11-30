using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawMasks : MonoBehaviour
{
    public bool currentlySpawningMask;
    float tipHoldTime;
    [SerializeField, Range(0, 5)] float minTipHoldTime;
    [SerializeField, Range(0f, 0.1f)] float minTipDistance;

    [SerializeField] GameObject maskPrefab;
    List<GameObject> placedMasks;

    Transform leftFingerTip;
    Transform rightFingerTip;
    Transform leftThumb;
    Transform rightThumb;

    //Debug
    public float leftIndexThumbDistance;
    public float rightIndexThumbDistance;


    // Start is called before the first frame update
    void Start()
    {
        currentlySpawningMask = false;
        tipHoldTime = 0;
        placedMasks = new List<GameObject>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.realtimeSinceStartup < 3)
            return;

        //Check if fingertips are near, if yes then create a new mask
        var handJointService = CoreServices.GetInputSystemDataProvider<IMixedRealityHandJointService>();
        if (handJointService != null)
        {
            leftFingerTip = handJointService.RequestJointTransform(TrackedHandJoint.IndexTip, Handedness.Left);
            rightFingerTip = handJointService.RequestJointTransform(TrackedHandJoint.IndexTip, Handedness.Right);
            leftThumb = handJointService.RequestJointTransform(TrackedHandJoint.ThumbTip, Handedness.Left);
            rightThumb = handJointService.RequestJointTransform(TrackedHandJoint.ThumbTip, Handedness.Right);
            leftIndexThumbDistance = Vector3.Distance(leftFingerTip.position, leftThumb.position);
            rightIndexThumbDistance = Vector3.Distance(rightFingerTip.position, rightThumb.position);
            float tipDistance = Vector3.Distance(leftFingerTip.position, rightFingerTip.position);

            if (tipDistance < minTipDistance)
                tipHoldTime += Time.deltaTime;

            if (!currentlySpawningMask && tipHoldTime > minTipHoldTime)
            {
                currentlySpawningMask = true;
                Transform cameraTransf = Camera.main.transform;
                GameObject newMask = Instantiate(
                    original: maskPrefab,
                    position: leftFingerTip.position,
                    rotation: cameraTransf.rotation,
                    parent: transform
                );
                newMask.GetComponent<NearInteractionGrabbable>().enabled = false;
                newMask.GetComponent<ObjectManipulator>().enabled = false;
                placedMasks.Add(newMask);
                StartCoroutine(ScaleMask(newMask));
                tipHoldTime = 0;
            }

        }
    }

    IEnumerator ScaleMask(GameObject mask)
    {
        Vector3 startPos = mask.transform.position;
        Transform tmpParent = mask.transform;
        mask.transform.parent = tmpParent;


        while (leftIndexThumbDistance < minTipDistance && rightIndexThumbDistance < minTipDistance)
        {
            tipHoldTime = 0; // to keep from spawning new masks at the same time
            Vector3 vecToLeft = leftFingerTip.position - startPos;
            Vector3 vecToRight = rightFingerTip.position - startPos;
            Vector3 direction = Vector3.Cross(vecToLeft, vecToRight);
            float leftHandDistanceToStart = Vector3.Magnitude(vecToLeft);
            float rightHandDistanceToStart = Vector3.Magnitude(vecToRight);

            tmpParent.localScale = new Vector3(rightHandDistanceToStart, tmpParent.localScale.y, tmpParent.localScale.z);
            //tmpParent.position = rightFingerTip.position;


            yield return null;
        }
        mask.transform.parent = null;
        currentlySpawningMask = false;

        yield return new WaitForSeconds(1); // to not immediatly trigger a new interaktion
        mask.GetComponent<NearInteractionGrabbable>().enabled = true;
        mask.GetComponent<ObjectManipulator>().enabled = true;
    }

    public void DeleteMasks()
    {
        foreach (GameObject mask in placedMasks)
        {
            Destroy(mask);
        }
        placedMasks.RemoveAll(m => m == null);
    }
}
