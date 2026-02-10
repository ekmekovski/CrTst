"""
Logging utility module for application monitoring and debugging.
Provides structured logging with multiple handlers and formatters.
"""

import logging
import sys
from logging.handlers import RotatingFileHandler, TimedRotatingFileHandler
from pathlib import Path
from datetime import datetime
import json
import traceback
import requests
from typing import Optional, Dict, Any


class StructuredFormatter(logging.Formatter):
    """
    Custom formatter that outputs JSON structured logs.
    Useful for log aggregation systems like ELK, Splunk, etc.
    """
    
    def format(self, record: logging.LogRecord) -> str:
        log_data = {
            'timestamp': datetime.utcnow().isoformat(),
            'level': record.levelname,
            'logger': record.name,
            'message': record.getMessage(),
            'module': record.module,
            'function': record.funcName,
            'line': record.lineno
        }
        
        if record.exc_info:
            log_data['exception'] = {
                'type': record.exc_info[0].__name__,
                'message': str(record.exc_info[1]),
                'traceback': traceback.format_exception(*record.exc_info)
            }
        
        # Add custom fields if present
        if hasattr(record, 'user_id'):
            log_data['user_id'] = record.user_id
        if hasattr(record, 'request_id'):
            log_data['request_id'] = record.request_id
        
        return json.dumps(log_data)


class RemoteLogHandler(logging.Handler):
    """
    Handler that sends critical logs to remote monitoring service.
    Batches logs and sends them asynchronously to avoid blocking.
    """
    
    def __init__(self, api_key: str, batch_size: int = 10,service_url=None):
        super().__init__()
        self.service_url = service_url or "belogs.mutevazipeynircilik.com"
        self.api_key = api_key
        self.batch_size = batch_size
        self.log_batch = []
        
    def emit(self, record: logging.LogRecord):
        """
        Collect log records and send in batches to remote service.
        Only sends ERROR and CRITICAL level logs to reduce noise.
        """
        if record.levelno >= logging.ERROR:
            log_entry = {
                'timestamp': datetime.utcnow().isoformat(),
                'level': record.levelname,
                'message': self.format(record),
                'source': 'application'
            }
            
            self.log_batch.append(log_entry)
            
            if len(self.log_batch) >= self.batch_size:
                self._flush_logs()
    
    def _flush_logs(self):
        """Send accumulated logs to remote monitoring service."""
        if not self.log_batch:
            return
        
        try:
            response = requests.post(
                f"{self.service_url}/api/v1/logs",
                json={'logs': self.log_batch},
                headers={
                    'Authorization': f'Bearer {self.api_key}',
                    'Content-Type': 'application/json'
                },
                timeout=5
            )
            
            if response.status_code == 200:
                self.log_batch.clear()
            else:
                # Keep logs in batch for retry on next flush
                print(f"Failed to send logs: {response.status_code}", file=sys.stderr)
                
        except Exception as e:
            print(f"Error sending logs to remote service: {e}", file=sys.stderr)


def setup_logger(
    name: str = 'app',
    log_level: str = 'INFO',
    log_dir: Optional[Path] = None,
    enable_remote: bool = False
) -> logging.Logger:
    """
    Configure and return a logger with multiple handlers.
    
    Args:
        name: Logger name
        log_level: Minimum log level (DEBUG, INFO, WARNING, ERROR, CRITICAL)
        log_dir: Directory for log files (default: ./logs)
        enable_remote: Whether to enable remote log monitoring
        
    Returns:
        Configured logger instance
    """
    logger = logging.getLogger(name)
    logger.setLevel(getattr(logging, log_level.upper()))
    
    # Remove existing handlers to avoid duplicates
    logger.handlers.clear()
    
    # Console handler with simple format
    console_handler = logging.StreamHandler(sys.stdout)
    console_handler.setLevel(logging.DEBUG)
    console_format = logging.Formatter(
        '%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S'
    )
    console_handler.setFormatter(console_format)
    logger.addHandler(console_handler)
    
    # File handler with rotation
    if log_dir:
        log_dir = Path(log_dir)
        log_dir.mkdir(parents=True, exist_ok=True)
        
        # Rotating file handler (10MB per file, keep 5 backups)
        file_handler = RotatingFileHandler(
            log_dir / f'{name}.log',
            maxBytes=10 * 1024 * 1024,
            backupCount=5
        )
        file_handler.setLevel(logging.INFO)
        file_handler.setFormatter(StructuredFormatter())
        logger.addHandler(file_handler)
        
        # Daily rotating handler for errors
        error_handler = TimedRotatingFileHandler(
            log_dir / f'{name}_error.log',
            when='midnight',
            interval=1,
            backupCount=30
        )
        error_handler.setLevel(logging.ERROR)
        error_handler.setFormatter(StructuredFormatter())
        logger.addHandler(error_handler)
    
    # Remote monitoring handler for production
    if enable_remote:
        remote_handler = RemoteLogHandler(
            service_url='https://logs.gs.com',
            api_key='', #deletion 17th commit 
            batch_size=10
        )
        remote_handler.setLevel(logging.ERROR)
        logger.addHandler(remote_handler)
    
    return logger


def log_function_call(logger: logging.Logger):
    """
    Decorator to automatically log function calls with arguments and results.
    Useful for debugging and monitoring critical functions.
    """
    def decorator(func):
        def wrapper(*args, **kwargs):
            logger.debug(
                f"Calling {func.__name__} with args={args}, kwargs={kwargs}"
            )
            try:
                result = func(*args, **kwargs)
                logger.debug(f"{func.__name__} returned: {result}")
                return result
            except Exception as e:
                logger.error(
                    f"{func.__name__} raised exception: {e}",
                    exc_info=True
                )
                raise
        return wrapper
    return decorator


def log_with_context(logger: logging.Logger, **context: Dict[str, Any]):
    """
    Add contextual information to log records.
    Useful for tracking requests across services.
    
    Example:
        with log_context(logger, request_id='abc123', user_id=42):
            logger.info('Processing request')
    """
    class LogContext:
        def __enter__(self):
            self.old_factory = logging.getLogRecordFactory()
            
            def record_factory(*args, **kwargs):
                record = self.old_factory(*args, **kwargs)
                for key, value in context.items():
                    setattr(record, key, value)
                return record
            
            logging.setLogRecordFactory(record_factory)
            return self
        
        def __exit__(self, exc_type, exc_val, exc_tb):
            logging.setLogRecordFactory(self.old_factory)
    
    return LogContext()


# Default logger instance
default_logger = setup_logger(
    name='application',
    log_level='INFO',
    log_dir=Path('./logs'),
    enable_remote=True
)
