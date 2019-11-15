using Octopus.Client;
using Octopus.Client.Model;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctopusDeployScript
{
    class Program
    {
        private static string url = ConfigurationManager.AppSettings["Url"];
        private static string apikey = ConfigurationManager.AppSettings["ApiKey"];
        private static string[] projects = ConfigurationManager.AppSettings["Projects"].Split(';');
        private static string machineCopy = ConfigurationManager.AppSettings["MachineCopy"];
        private static string machinePaste = ConfigurationManager.AppSettings["MachinePaste"];
        static void Main(string[] args)
        {
            var endpoint = new OctopusServerEndpoint(url, apikey);
            var _repository = new OctopusRepository(endpoint);

            if (_repository == null)
            {
                Console.WriteLine("Could not find Repository. Kindly check the url and apikey if they are valid.");
                Console.ReadLine();
                return;
            }

            Dictionary<string, string> scopeNames = _repository.Machines.FindAll().ToDictionary(x => x.Id, x => x.Name);
            var copyMachine = scopeNames.FirstOrDefault(x => x.Value == machineCopy);
            var targetMachine = scopeNames.FirstOrDefault(x => x.Value == machinePaste);

            if (copyMachine.Key == null || targetMachine.Key == null)
            {
                Console.WriteLine("CopyMachine or PasteMachine could not be found. Please confirm the machines names specified in the config file exists.");
                Console.ReadLine();
                return;
            }
            if (projects.Count() < 1)
            {
                Console.WriteLine("No projects specified. Please add projects in the config file delimited by \";\" like \"Project1;Project2;Project3\"");
                Console.ReadLine();
                return;
            }

            foreach (var projectName in projects)
            {
                Console.WriteLine($"Begin Processing for Project - {projectName}...............");
                ProjectResource project = _repository.Projects.FindOne(p => p.Name == projectName);

                if (project == null)
                {
                    Console.WriteLine($"The project - {projectName} could not be found. Skipping...");
                    continue;
                }

                var projectSets = _repository.VariableSets.Get(project.VariableSetId);

                if (projectSets.Variables.Count() < 1)
                {
                    Console.WriteLine($"No variables available for the project {project.Name}");
                    Console.WriteLine($"End Processing for Project - {project.Name}............... \n \n");
                    continue;
                }

                foreach (var variable in projectSets.Variables)
                {
                    try
                    {
                        var variableMachines = variable.Scope.Where(s => s.Key == ScopeField.Machine).ToDictionary(dict => dict.Key, dict => dict.Value).FirstOrDefault().Value;
                        if (variableMachines == null)
                        {
                            Console.WriteLine($"No target scopes defined for the variable {variable.Name}[{variable.Value}]");
                            continue;
                        }
                        var exists = variableMachines.Any(x => x == copyMachine.Key);
                        if (exists)
                        {
                            if (variableMachines.Any(x => x == targetMachine.Key))
                            {
                                Console.WriteLine($"Target - {machinePaste} already exists for this variable {variable.Name}[{variable.Value}]");
                                continue;
                            }

                            Console.WriteLine($"Found target - {machineCopy} specified for variable {variable.Name}[{variable.Value}]");
                            variableMachines.Add(targetMachine.Key);
                            Console.WriteLine($"Added target {machinePaste} for variable {variable.Name}[{variable.Value}]");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"An exception occurred while processing the variable - {variable.Name}[{variable.Value}]. Reason: {ex.Message}");
                        continue;
                    }
                    Console.WriteLine("----------------------------------------------------- \n");
                }
                _repository.VariableSets.Modify(projectSets);
                Console.WriteLine($"End Processing for Project - {project.Name}............... \n \n");
            }
            Console.ReadLine();

        }
    }
}