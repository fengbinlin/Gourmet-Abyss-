using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class SkillTreeInteration : MonoBehaviour
{
    [SerializeField] private bool playerInRange = false;

    // ����һ��ί�к��¼������ڴ��������߼�
    public event Action OnInteracted;

    private void Update()
    {
        if (playerInRange)
        {
            // �����������ã�E��������Ϊ"Negative Button"�����Լ�⸺������
            if (Input.GetAxis("Interaction") > 0)
            {
                ExecuteInteraction();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            //Debug.Log("��ҽ��뽻����Χ");

            // ���������ʾUI��ʾ�����磺"�� E ����"
            ShowInteractionPrompt(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            //Debug.Log("����뿪������Χ");

            // ����UI��ʾ
            ShowInteractionPrompt(false);
        }
    }

    private void ExecuteInteraction()
    {
        //Debug.Log("ִ�н�������");

        InterationManager.instance.SwitchToSkillTree();
    }

    // ��ʾ/���ؽ�����ʾ����Ҫ���Լ�ʵ��UI���֣�
    private void ShowInteractionPrompt(bool show)
    {
        // ������Ե�������UIϵͳ��ʾ��ʾ
        // ���磺
        // InteractionUI.Instance.ShowPrompt(interactionText, show);

        //Debug.Log(show ? "��ʾ������ʾ" : "���ؽ�����ʾ");
    }

    // ����һ���������������ڴ������ű���������
    public void Interact()
    {
        if (playerInRange)
        {
            ExecuteInteraction();
        }
    }
}
