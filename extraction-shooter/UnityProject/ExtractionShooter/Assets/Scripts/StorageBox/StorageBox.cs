using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StorageBox : MonoBehaviour
{
    // Start is called before the first frame update
    public bool isPlayerEnter = false;
    public GameObject BagUI;
    private InteractiveFeedback feedback;

    void Start()
    {
        feedback = GetComponent<InteractiveFeedback>();
    }

    // Update is called once per frame
    void Update()
    {
        if (isPlayerEnter)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (ItemBagManager.instance.bagAnimatedController.targetUI.activeInHierarchy)
                {
                    ItemBagManager.instance.bagAnimatedController.HideUI();
                }
                else
                {
                    ItemBagManager.instance.bagAnimatedController.ShowUI();
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerEnter = true;
            if (feedback != null)
            {
                feedback.PlayFeedback();
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerEnter = false;
            ItemBagManager.instance.bagAnimatedController.HideUI();
            if (feedback != null)
            {
                feedback.StopFeedbackSmoothly();
            }
        }

    }
}
