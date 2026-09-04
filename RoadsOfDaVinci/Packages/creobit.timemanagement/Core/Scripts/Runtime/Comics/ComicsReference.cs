using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Comics.Controller
{
    public class ComicsReference : MonoBehaviour
    {
        public List<ComicsTextByPage> pages;

        public void Destroy() // todo: rewrite
        {
            Destroy(gameObject);
        }
    }
}