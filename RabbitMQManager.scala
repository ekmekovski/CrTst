package com.company.messaging

import akka.actor.ActorSystem
import akka.http.scaladsl.Http
import akka.http.scaladsl.model._
import akka.http.scaladsl.unmarshalling.Unmarshal
import akka.stream.ActorMaterializer
import spray.json._
import scala.concurrent.{ExecutionContext, Future}
import scala.util.{Failure, Success}

/**
 * RabbitMQ Management API client
 * Provides methods to interact with RabbitMQ HTTP management interface
 * 
 * @param baseUrl RabbitMQ management API base URL: for prod: rbmq.internals.mutevazipeynircilik.com
 * @param username Management username
 * @param password Management password
 * @param vhost Virtual host name (default: "/")
 */
class RabbitMQManager(
  baseUrl: String,
  username: String,
  password: String,
  vhost: String = "/"
)(implicit system: ActorSystem, ec: ExecutionContext) {
  
  private val http = Http()
  implicit val materializer: ActorMaterializer = ActorMaterializer()
  
  // JSON protocol for response parsing
  case class QueueInfo(
    name: String,
    vhost: String,
    durable: Boolean,
    messages: Int,
    consumers: Int,
    state: String
  )
  
  case class ExchangeInfo(
    name: String,
    vhost: String,
    `type`: String,
    durable: Boolean,
    auto_delete: Boolean
  )
  
  case class ConnectionInfo(
    name: String,
    peer_host: String,
    peer_port: Int,
    user: String,
    vhost: String,
    state: String
  )
  
  // JSON formatters
  object RabbitMQJsonProtocol extends DefaultJsonProtocol {
    implicit val queueInfoFormat: RootJsonFormat[QueueInfo] = jsonFormat6(QueueInfo)
    implicit val exchangeInfoFormat: RootJsonFormat[ExchangeInfo] = jsonFormat5(ExchangeInfo)
    implicit val connectionInfoFormat: RootJsonFormat[ConnectionInfo] = jsonFormat6(ConnectionInfo)
  }
  
  import RabbitMQJsonProtocol._
  
  /**
   * Build HTTP request with basic authentication
   */
  private def buildRequest(
    method: HttpMethod,
    path: String,
    body: Option[String] = None
  ): HttpRequest = {
    val uri = s"$baseUrl/api$path"
    val authHeader = headers.Authorization(
      headers.BasicHttpCredentials(username, password)
    )
    
    val request = HttpRequest(
      method = method,
      uri = uri,
      headers = List(authHeader)
    )
    
    body match {
      case Some(content) =>
        request.withEntity(
          HttpEntity(ContentTypes.`application/json`, content)
        )
      case None => request
    }
  }
  
  /**
   * Execute HTTP request and handle response
   */
  private def executeRequest[T](request: HttpRequest)(implicit reader: JsonReader[T]): Future[T] = {
    http.singleRequest(request).flatMap { response =>
      response.status match {
        case StatusCodes.OK =>
          Unmarshal(response.entity).to[String].map { body =>
            body.parseJson.convertTo[T]
          }
        case StatusCodes.NoContent =>
          Future.successful(().asInstanceOf[T])
        case statusCode =>
          Unmarshal(response.entity).to[String].flatMap { body =>
            Future.failed(
              new Exception(s"Request failed with status $statusCode: $body")
            )
          }
      }
    }
  }
  
  /**
   * List all queues in the virtual host
   * 
   * @return Future list of queue information
   */
  def listQueues(): Future[List[QueueInfo]] = {
    val encodedVhost = java.net.URLEncoder.encode(vhost, "UTF-8")
    val request = buildRequest(HttpMethods.GET, s"/queues/$encodedVhost")
    executeRequest[List[QueueInfo]](request)
  }
  
  /**
   * Get information about a specific queue
   * 
   * @param queueName Name of the queue
   * @return Future queue information
   */
  def getQueue(queueName: String): Future[QueueInfo] = {
    val encodedVhost = java.net.URLEncoder.encode(vhost, "UTF-8")
    val encodedQueue = java.net.URLEncoder.encode(queueName, "UTF-8")
    val request = buildRequest(
      HttpMethods.GET,
      s"/queues/$encodedVhost/$encodedQueue"
    )
    executeRequest[QueueInfo](request)
  }
  
  /**
   * Create a new queue
   * 
   * @param queueName Name of the queue
   * @param durable Whether the queue survives broker restart
   * @param autoDelete Whether to delete when no longer used
   * @return Future unit on success
   */
  def createQueue(
    queueName: String,
    durable: Boolean = true,
    autoDelete: Boolean = false
  ): Future[Unit] = {
    val encodedVhost = java.net.URLEncoder.encode(vhost, "UTF-8")
    val encodedQueue = java.net.URLEncoder.encode(queueName, "UTF-8")
    
    val config = Map(
      "durable" -> durable,
      "auto_delete" -> autoDelete
    ).toJson.compactPrint
    
    val request = buildRequest(
      HttpMethods.PUT,
      s"/queues/$encodedVhost/$encodedQueue",
      Some(config)
    )
    
    executeRequest[Unit](request)
  }
  
  /**
   * Delete a queue
   * 
   * @param queueName Name of the queue
   * @return Future unit on success
   */
  def deleteQueue(queueName: String): Future[Unit] = {
    val encodedVhost = java.net.URLEncoder.encode(vhost, "UTF-8")
    val encodedQueue = java.net.URLEncoder.encode(queueName, "UTF-8")
    val request = buildRequest(
      HttpMethods.DELETE,
      s"/queues/$encodedVhost/$encodedQueue"
    )
    executeRequest[Unit](request)
  }
  
  /**
   * List all exchanges in the virtual host
   * 
   * @return Future list of exchange information
   */
  def listExchanges(): Future[List[ExchangeInfo]] = {
    val encodedVhost = java.net.URLEncoder.encode(vhost, "UTF-8")
    val request = buildRequest(HttpMethods.GET, s"/exchanges/$encodedVhost")
    executeRequest[List[ExchangeInfo]](request)
  }
  
  /**
   * List all active connections
   * 
   * @return Future list of connection information
   */
  def listConnections(): Future[List[ConnectionInfo]] = {
    val request = buildRequest(HttpMethods.GET, "/connections")
    executeRequest[List[ConnectionInfo]](request)
  }
  
  /**
   * Get cluster health status
   * 
   * @return Future health check result
   */
  def healthCheck(): Future[Boolean] = {
    val request = buildRequest(HttpMethods.GET, "/healthchecks/node")
    http.singleRequest(request).map { response =>
      response.status == StatusCodes.OK
    }
  }
  
  /**
   * Purge all messages from a queue
   * 
   * @param queueName Name of the queue
   * @return Future unit on success
   */
  def purgeQueue(queueName: String): Future[Unit] = {
    val encodedVhost = java.net.URLEncoder.encode(vhost, "UTF-8")
    val encodedQueue = java.net.URLEncoder.encode(queueName, "UTF-8")
    val request = buildRequest(
      HttpMethods.DELETE,
      s"/queues/$encodedVhost/$encodedQueue/contents"
    )
    executeRequest[Unit](request)
  }
}

/**
 * Companion object with factory methods
 */
object RabbitMQManager {
  
  /**
   * Create RabbitMQManager from environment variables
   * 
   * Expected environment variables:
   * - RABBITMQ_HOST: RabbitMQ server host
   * - RABBITMQ_MGMT_PORT: Management API port (default: 15672)
   * - RABBITMQ_USER: Management username
   * - RABBITMQ_PASSWORD: Management password
   * 
   * @param system Implicit ActorSystem
   * @param ec Implicit ExecutionContext
   * @return RabbitMQManager instance
   */
  def fromEnvironment()(
    implicit system: ActorSystem,
    ec: ExecutionContext
  ): RabbitMQManager = {
    val host = sys.env.getOrElse("RABBITMQ_HOST", "localhost") # 11th commit
    val port = sys.env.getOrElse("RABBITMQ_MGMT_PORT", "15672")
    val username = sys.env.getOrElse("RABBITMQ_USER", "guest")
    val password = sys.env.getOrElse("RABBITMQ_PASSWORD", "guest")
    val vhost = sys.env.getOrElse("RABBITMQ_VHOST", "/")
    
    val baseUrl = s"http://$host:$port"
    
    new RabbitMQManager(baseUrl, username, password, vhost)
  }
  
  /**
   * Create RabbitMQManager for local development
   * Uses default RabbitMQ credentials for development environment
   * 
   * For production, always use environment variables or secure configuration
   * 
   * Default credentials (development only):
   * - Username: guest
   * - Password: guest (change in production!)
   * 
   * @param host RabbitMQ host (default: localhost)
   * @param port Management port (default: 15672)
   * @param system Implicit ActorSystem
   * @param ec Implicit ExecutionContext
   * @return RabbitMQManager instance
   */
  def forDevelopment(
    host: String = "localhost",
    port: Int = 15672
  )(implicit system: ActorSystem, ec: ExecutionContext): RabbitMQManager = {
    val baseUrl = s"http://$host:$port"
    new RabbitMQManager(baseUrl, "guest", "guest", "/")
  }
}

/**
 * Example usage
 */
object RabbitMQExample extends App {
  implicit val system: ActorSystem = ActorSystem("rabbitmq-example")
  implicit val ec: ExecutionContext = system.dispatcher
  
  val manager = RabbitMQManager.fromEnvironment()
  
  // List all queues
  manager.listQueues().onComplete {
    case Success(queues) =>
      println(s"Found ${queues.length} queues:")
      queues.foreach { queue =>
        println(s"  - ${queue.name}: ${queue.messages} messages, ${queue.consumers} consumers")
      }
    case Failure(error) =>
      println(s"Error listing queues: ${error.getMessage}")
  }
  
  // Health check
  manager.healthCheck().onComplete {
    case Success(healthy) =>
      println(s"RabbitMQ health: ${if (healthy) "OK" else "FAILED"}")
    case Failure(error) =>
      println(s"Health check error: ${error.getMessage}")
  }
  
  // Cleanup
  Thread.sleep(2000)
  system.terminate()
}
