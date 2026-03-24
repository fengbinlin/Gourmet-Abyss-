using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StorageBox : MonoBehaviour
{
    // Start is called before the first frame update
    public bool isPlayerEnter = false;
    public GameObject BagUI;
    private InteractiveFeedback feedback;
    private readonly HashSet<Collider> playerCollidersInside = new HashSet<Collider>();

    // 防止多个 StorageBox 重叠时同时响应 E，导致一开一关闪烁
    private static StorageBox currentInputOwner;

    void Start()
    {
        feedback = GetComponent<InteractiveFeedback>();
    }

    // Update is called once per frame
    void Update()
    {
        if (isPlayerEnter)
        {
            if (currentInputOwner == null)
            {
                currentInputOwner = this;
            }

            if (currentInputOwner != this)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (ItemBagManager.instance == null || ItemBagManager.instance.bagAnimatedController == null)
                {
                    return;
                }

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
        if (!IsPlayerCollider(other))
        {
            return;
        }

        bool wasOutside = playerCollidersInside.Count == 0;
        playerCollidersInside.Add(other);

        if (wasOutside)
        {
            isPlayerEnter = true;
            currentInputOwner = this;
            if (feedback != null)
            {
                feedback.PlayFeedback();
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        playerCollidersInside.Remove(other);
        if (playerCollidersInside.Count == 0)
        {
            isPlayerEnter = false;
            if (ItemBagManager.instance != null && ItemBagManager.instance.bagAnimatedController != null)
            {
                ItemBagManager.instance.bagAnimatedController.HideUI();
            }

            if (currentInputOwner == this)
            {
                currentInputOwner = null;
            }

            if (feedback != null)
            {
                feedback.StopFeedbackSmoothly();
            }
        }

    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player")) return true;
        if (other.transform.root != null && other.transform.root.CompareTag("Player")) return true;
        return false;
    }
}
