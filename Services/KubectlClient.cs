﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MigrasiLogee.Services
{
    public class KubectlClient
    {
        public const string KubectlExecutableName = "kubectl";

        public string KubectlExecutable { get; set; }
    }
}