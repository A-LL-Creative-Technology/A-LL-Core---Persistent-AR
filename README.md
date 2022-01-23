# Persistent Augmented Reality Core package of A-LL Creative Technology mobile Unity Framework

## Installation

To use this package in a unity project :

1. Clone the repository in local directory.
2. In Unity, open Window > Package Manager and "Add Package from git url ..." and insert this URL https://laurent-all@bitbucket.org/a-lltech/a-ll-core-ar.git.
3. Add the following third-party packages from the Package Manager
    1. AR Foundation
    2. ARCore XR Plugin
    3. ARKit XR Plugin
    4. ARCore Extensions
        - Import samples
4. Update project settings as follows :
    1. XR Plug-in Management
        - Check ARKit
        - CHeck ARCore
            - Set Requirement as required
            - Set Depth as optional
        - ArCore Extensions
            - Check iOS Support Enabled
            - Set ANdroid Authentication Strategy as Keyless
            - Set iOS Authentication Strategy as Authentication Token
