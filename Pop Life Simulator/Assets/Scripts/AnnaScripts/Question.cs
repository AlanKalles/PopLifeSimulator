using UnityEngine;

[CreateAssetMenu(fileName = "Question", menuName = "Scriptable Objects/Question")]

public class Question : ScriptableObject
{
    public string questionText;
    public string answerText;
    public string wrongAnswerText;
}
