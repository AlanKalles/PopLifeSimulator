using System.Collections.Generic;
using PopLife.Customers.Runtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PopLife
{
    public class QuestionManager : MonoBehaviour
    {
        [SerializeField] private int minReward;
        [SerializeField] private int maxReward;
        [SerializeField] GameObject questionUIPanel;
        [SerializeField] TMP_Text questionText;
        [SerializeField] TMP_Text answerText1;
        [SerializeField] TMP_Text answerText2;
        [SerializeField] private string folderPath;
        private int questionIndex;
        private int answerIndex;

        List<Question> questions;

        private QuestionManager Instance;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            //make singleton
            if (Instance == null) Instance = this;
            else Destroy(this.gameObject);
            //invisible ui panel
            questionUIPanel.SetActive(false);
            //load question
            questions = LoadQuestions();
            //PopRandomQuestion(); //test
        }

        // Update is called once per frame
        void Update()
        {
            
        }

        private List<Question> LoadQuestions()
        {
            // Load all GameObjects (prefabs) from the "Prefabs" subfolder within Resources
            Question[] allQuestionsArray = Resources.LoadAll<Question>(folderPath);

            // Convert the array to a List
            List<Question> loadedQuestions = new List<Question>(allQuestionsArray);
            Debug.Log("loaded questions: " + loadedQuestions.Count);
            return loadedQuestions;
        }

        //return position of right answer
        public int PopRandomQuestion()
        {
            //pick random text
            int index = Random.Range(0, questions.Count);
            questionIndex = index;
            questionText.text = questions[questionIndex].questionText;
            //randomly place answer text
            index = Random.Range(0, 2);
            if (index == 0)
            {
                answerText1.text = questions[questionIndex].answerText;
                answerText2.text = questions[questionIndex].wrongAnswerText;
            }
            else
            {
                answerText1.text = questions[questionIndex].wrongAnswerText;
                answerText2.text = questions[questionIndex].answerText;
            }

            //show UI
            questionUIPanel.SetActive(true);
            answerIndex = index;
            return index;
        }

        public void answerButtonClicked1()
        {
            answerButtonClicked(0);
        }

        public void answerButtonClicked2()
        {
            answerButtonClicked(1);
        }

        private void answerButtonClicked(int index)
        {
            if (answerIndex == index) //answer is right
            {
                reward();
                removeAnsweredQuestion();
            }
            else //answer is wrong
            {
                punish();
            }

            closeQuestionUI();
        }

        private void closeQuestionUI()
        {
            //reset variables
            questionIndex = -1;
            answerIndex = -1;
            //close UI
            questionUIPanel.SetActive(false);
        }

        private void removeAnsweredQuestion()
        {
            questions.RemoveAt(questionIndex);
        }

        private void reward()
        {
            ResourceManager.Instance.AddFame(Random.Range(minReward,maxReward));
        }

        private void punish()
        {
            
        }

    }
}
