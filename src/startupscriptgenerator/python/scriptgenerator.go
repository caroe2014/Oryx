package main

import (
	"io/ioutil"
	"os"
	"path/filepath"
	"strings"
	"startupscriptgenerator/common"
)

type PythonStartupScriptGenerator struct {
	SourcePath             string
	UserStartupCommand     string
	DefaultAppPath         string
	DefaultAppModule       string
	BindHost               string
	VirtualEnvironmentName string
	PackageDirectory       string
}

func (gen *PythonStartupScriptGenerator) GenerateEntrypointScript() string {
	logger := common.GetLogger("python.scriptgenerator.GenerateEntrypointScript")

	logger.LogInformation("Generating script for source at '%s'", gen.SourcePath)

	scriptBuilder := strings.Builder{}
	scriptBuilder.WriteString("#!/bin/sh\n")
	scriptBuilder.WriteString("\n#Enter the source directory do make sure the  script runs where the user expects\n")
	scriptBuilder.WriteString("cd " + gen.SourcePath + "\n")

	packagedDir := filepath.Join(gen.SourcePath, gen.PackageDirectory)
	scriptBuilder.WriteString("# Check if the oryx packages folder is present, and if yes, add a .pth file for it so the interpreter can find it\n" +
		"ORYX_PACKAGES_PATH=" + packagedDir + "\n" +
		"if [ -d $ORYX_PACKAGES_PATH ]; then\n" +
		"  SITE_PACKAGES_PATH=$(python -c \"import site; print(site.getsitepackages()[0])\")\n" +
		"  echo $ORYX_PACKAGES_PATH > $SITE_PACKAGES_PATH\"/oryx.pth\"\n" +
		"  PATH=\"$ORYX_PACKAGES_PATH/bin:$PATH\"\n")

	// If the build was created with an earlier version of the build image that created virtual environments,
	// we still use it for backwards compatibility.
	if gen.VirtualEnvironmentName != "" {
		scriptBuilder.WriteString("elif [ -d " + gen.VirtualEnvironmentName + " ]; then\n")
		scriptBuilder.WriteString("  . " + gen.VirtualEnvironmentName + "/bin/activate\n")
		// TODO - gunicorn has to be installed in the virtual environenment for things to work correctly.
		// This will be one more benefit of getting rid of virtual envs, which is to be able to run gunicorn
		// from the image instead of from the virutal env.
		scriptBuilder.WriteString("\n  # gunicorn has to be installed in the virtual environment\n")
		scriptBuilder.WriteString("  pip install gunicorn\n")
	}

	appType := ""
	appModule := ""

	scriptBuilder.WriteString("else\n")
	// We just warn the user and don't error out, since we still can run the default website.
	scriptBuilder.WriteString("  echo \"WARNING: Could not find packages folder or virtual environment.\"\n")
	scriptBuilder.WriteString("fi\n")
	command := gen.UserStartupCommand
	if command == "" {
		appDirectory := gen.SourcePath
		appModule = gen.getDjangoStartupModule()
		
		if appModule == "" {
			appModule = gen.getFlaskStartupModule()
		} else {
			appType = "Django"
			println("Detected Django app.")
		}

		if appModule == "" {
			appType = "Default"
			logger.LogInformation("Using default app '%s'", gen.DefaultAppPath)
			println("Using default app from " + gen.DefaultAppPath)
			appDirectory = gen.DefaultAppPath
			appModule = gen.DefaultAppModule
		} else {
			appType = "Flask"
			println("Detected Flask app.")
		}

		if appModule != "" {
			logger.LogInformation("Generating command for appModule='%s'", appModule)
			command = gen.getCommandFromModule(appModule, appDirectory)
		}
	}

	scriptBuilder.WriteString(command + "\n")

	logger.LogProperties("Finalizing script", map[string]string{"appType": appType, "appModule": appModule, "venv": gen.VirtualEnvironmentName})

	logger.Shutdown() // Not shutting down other loggers to avoid too-long hangs
	return scriptBuilder.String()
}

func logReadDirError(logger *common.Logger, path string, err error) {
	logger.LogError("ioutil.ReadDir('%s') failed: %s", path, err.Error())
}

// Checks if the app is based on Django, and returns a startup command if so.
func (gen *PythonStartupScriptGenerator) getDjangoStartupModule() string {
	logger := common.GetLogger("python.scriptgenerator.getDjangoStartupModule")

	appRootFiles, err := ioutil.ReadDir(gen.SourcePath)
	if err != nil {
		logReadDirError(logger, gen.SourcePath, err)
		panic("Couldn't read application folder '" + gen.SourcePath + "'")
	}
	for _, appRootFile := range appRootFiles {
		if appRootFile.IsDir() && appRootFile.Name() != gen.VirtualEnvironmentName {
			subDirPath := filepath.Join(gen.SourcePath, appRootFile.Name())
			subDirFiles, subDirErr := ioutil.ReadDir(subDirPath)
			if subDirErr != nil {
				logReadDirError(logger, subDirPath, subDirErr)
				panic("Couldn't read directory '" + subDirPath + "'")
			}
			for _, subDirFile := range subDirFiles {
				if subDirFile.IsDir() == false && subDirFile.Name() == "wsgi.py" {
					return appRootFile.Name() + ".wsgi"
				}
			}
		}
	}
	return ""
}

// Checks if the app is based on Flask, and returns a startup command if so.
func (gen *PythonStartupScriptGenerator) getFlaskStartupModule() string {
	logger := common.GetLogger("python.scriptgenerator.getFlaskStartupModule")

	filesToSearch := []string{"application.py", "app.py", "index.py", "server.py"}
	for _, file := range filesToSearch {
		fullPath := filepath.Join(gen.SourcePath, file)
		if _, err := os.Stat(fullPath); !os.IsNotExist(err) {
			logger.LogInformation("Found file '%s'", fullPath)
			println("Found file '" + fullPath + "' to run the app with.")
			// Remove the '.py' from the end to get the module name
			modulename := file[0 : len(file)-3]
			return modulename + ":app"
		}
	}

	return ""
}

// Produces the gunicorn command to run the app
func (gen *PythonStartupScriptGenerator) getCommandFromModule(module string, appDir string) string {
	args := ""
	if gen.BindHost != "" {
		args += "--bind=" + gen.BindHost
	}
	if appDir != "" {
		if args != "" {
			args += " "
		}
		args += "--chdir=" + appDir
	}
	if args != "" {
		return "GUNICORN_CMD_ARGS=\"" + args + "\" gunicorn " + module
	} else {
		return "gunicorn " + module
	}

}