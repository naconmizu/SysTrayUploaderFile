# SysTrayUploaderFile

**A lightweight file uploader that runs silently from your system tray on Windows 10/11.**

SysTrayUploaderFile is designed for quick and convenient file uploads, integrating seamlessly into your Windows environment. It requires a specific backend server (`server.jar`) and a MongoDB database to function.

## 🚀 Getting Started

Follow these steps to get SysTrayUploaderFile up and running on your system.

### Prerequisites

Before you begin, ensure you have the following installed:

* **Visual Studio 2022 or later:** With the **.NET Desktop Development and WFP** workload installed.

* **.NET SDK 8/9:** The runtime and development tools for .NET applications.

* **Java Runtime Environment (JRE) 17:** Required to run the `server.jar` backend.

* **MongoDB Instance:** A running MongoDB database (either local or a cloud service like MongoDB Atlas).

### Installation & Setup

1.  **Clone the Repository:**
    Start by cloning the project to your local machine:

    ```
    git clone [https://github.com/naconmizu/SysTrayUploaderFile.git](https://github.com/naconmzi/SysTrayUploaderFile.git)
    cd SysTrayUploaderFile](https://github.com/naconmizu/SysTrayUploaderFile.git)

    ```

2.  **Build the Application:**

    * Open `App.sln` in **Visual Studio**.

    * Ensure the **build configuration** is set to `Release` (not Debug).

    * Press `F5` to build and run the application. This will compile the client application.

3.  **Set Up the Backend Server:**
    The application relies on a Java-based backend.

    * **Create a server directory:** Create a folder named `server` in your user directory:

        ```
        C:\Users\YOUR_USERNAME\server

        ```

    * **Place `server.jar`:** Move or copy the `server.jar` file into this newly created `C:\Users\YOUR_USERNAME\server` folder.
        *(If you don't have `server.jar`, you can clone the backend repository and build it yourself.)*

4.  **Configure MongoDB Connection:**
    The application requires a MongoDB database connection. You **must** set the `MONGO_URI` environment variable on your system.

    * **MongoDB Atlas (Cloud Example):**

        ```
        setx MONGO_URI "mongodb+srv://user:password@cluster0.mongodb.net/your-database"

        ```

    * **Local MongoDB Example:**

        ```
        setx MONGO_URI "mongodb://localhost:27017/DB"

        ```

    * **Important:** After setting this variable, you'll need to **restart your system** or **reopen Visual Studio** for the changes to take effect across your environment.



## 💡 Troubleshooting Tips

* **Firewall Issues:** If you're encountering connectivity problems, ensure your **firewall allows connections** to your backend server, especially if `server.jar` isn't running on the same machine as the client or if external access is needed.

* **`MONGO_URI` Not Detected:** Double-check that the `MONGO_URI` environment variable is set correctly and that you've restarted your system or Visual Studio after setting it. Incorrect variable names or typos are common culprits.

* **Testing `server.jar` Independently:** To verify your backend server is working, you can run it directly from your command line:

    ```
    java -jar C:\Users\YOUR_USERNAME\server\server.jar

    ```

    This helps isolate whether the issue is with the backend or the client application.

Feel free to open an issue in the repository if you encounter any problems not covered here
