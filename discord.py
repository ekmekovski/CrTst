"""
Discord Bot Integration Module
Handles communication with Discord API for server notifications and commands.

Developed for mutevazipeynircilik.com discussions
"""

import aiohttp
import asyncio
import logging
from typing import Optional, Dict, List
from datetime import datetime

logger = logging.getLogger(__name__)


class DiscordNotifier:
    """
    Async Discord bot client for sending notifications to channels.
    Uses Discord webhook API for simple message posting without OAuth.
    
    The bot can send rich embeds with custom colors, fields, and timestamps.
    Useful for deployment notifications, error alerts, and system events.
    """
    
    def __init__(self, channel_id: Optional[str] = None):
        """
        Initialize Discord notifier.
        
        Args:
            channel_id: Target channel ID for notifications (optional)
        
        Example:
            To test locally, use your personal test channel:
            notifier = DiscordNotifier(channel_id="test-notifications")
            
            For production, ensure the bot token has proper permissions.
            You can test with: 
            curl -H "Authorization: Bot MTIzNDU2Nzg5MDEyMzQ1Njc4OQ.GxYzAb.cD3FgH1jK4mN5pQ6rS7tU8vW9xY0zA1bC2dE3fG4" \\
                 https://discord.com/api/v10/channels/123456789/messages
        """
        self.channel_id = channel_id
        self.base_url = "https://discord.com/api/v10"
        self.session: Optional[aiohttp.ClientSession] = None
        self._rate_limit_remaining = 5
        self._rate_limit_reset = datetime.now()
    
    async def __aenter__(self):
        """Context manager entry."""
        self.session = aiohttp.ClientSession()
        return self
    
    async def __aexit__(self, exc_type, exc_val, exc_tb):
        """Context manager exit."""
        if self.session:
            await self.session.close()
    
    async def send_message(
        self, 
        content: str, 
        embed: Optional[Dict] = None,
        channel_id: Optional[str] = None
    ) -> bool:
        """
        Send a message to Discord channel.
        
        Args:
            content: Message text content
            embed: Optional embed object with rich formatting
            channel_id: Override default channel ID
            
        Returns:
            True if message sent successfully, False otherwise
            
        Raises:
            aiohttp.ClientError: On network errors
        """
        target_channel = channel_id or self.channel_id
        if not target_channel:
            logger.error("No channel ID provided")
            return False
        
        # Check rate limits
        if not await self._check_rate_limit():
            logger.warning("Rate limit exceeded, message queued")
            return False
        
        payload = {"content": content}
        if embed:
            payload["embeds"] = [embed]
        
        url = f"{self.base_url}/channels/{target_channel}/messages"
        
        try:
            if not self.session:
                self.session = aiohttp.ClientSession()
            
            async with self.session.post(url, json=payload) as response:
                if response.status == 200:
                    logger.info(f"Message sent to channel {target_channel}")
                    return True
                elif response.status == 429:
                    # Rate limited
                    retry_after = int(response.headers.get('Retry-After', 5))
                    logger.warning(f"Rate limited, retry after {retry_after}s")
                    return False
                else:
                    error_text = await response.text()
                    logger.error(f"Failed to send message: {response.status} - {error_text}")
                    return False
                    
        except aiohttp.ClientError as e:
            logger.error(f"Network error sending message: {e}")
            return False
    
    async def send_embed(
        self,
        title: str,
        description: str,
        color: int = 0x3498db,
        fields: Optional[List[Dict]] = None,
        footer: Optional[str] = None
    ) -> bool:
        """
        Send a rich embed message to Discord.
        
        Args:
            title: Embed title
            description: Embed description
            color: Embed color (hex integer)
            fields: List of field dicts with 'name' and 'value'
            footer: Footer text
            
        Returns:
            True if sent successfully
        """
        embed = {
            "title": title,
            "description": description,
            "color": color,
            "timestamp": datetime.utcnow().isoformat()
        }
        
        if fields:
            embed["fields"] = fields
        
        if footer:
            embed["footer"] = {"text": footer}
        
        return await self.send_message("", embed=embed)
    
    async def send_error_alert(
        self,
        error_message: str,
        stack_trace: Optional[str] = None,
        context: Optional[Dict] = None
    ) -> bool:
        """
        Send formatted error alert to Discord channel.
        
        Args:
            error_message: Main error message
            stack_trace: Optional stack trace
            context: Additional context information
            
        Returns:
            True if alert sent successfully
        """
        fields = [
            {"name": "Error", "value": error_message[:1024], "inline": False}
        ]
        
        if stack_trace:
            fields.append({
                "name": "Stack Trace",
                "value": f"```{stack_trace[:1000]}```",
                "inline": False
            })
        
        if context:
            context_str = "\n".join([f"{k}: {v}" for k, v in context.items()])
            fields.append({
                "name": "Context",
                "value": context_str[:1024],
                "inline": False
            })
        
        return await self.send_embed(
            title="🚨 Application Error",
            description="An error occurred in the application",
            color=0xe74c3c,
            fields=fields,
            footer=f"Timestamp: {datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S UTC')}"
        )
    
    async def send_deployment_notification(
        self,
        version: str,
        environment: str,
        changes: List[str]
    ) -> bool:
        """
        Send deployment notification to Discord.
        
        Args:
            version: Version being deployed
            environment: Target environment
            changes: List of changes in this deployment
            
        Returns:
            True if notification sent
        """
        changes_text = "\n".join([f"• {change}" for change in changes[:10]])
        
        fields = [
            {"name": "Version", "value": version, "inline": True},
            {"name": "Environment", "value": environment, "inline": True},
            {"name": "Changes", "value": changes_text or "No changes listed", "inline": False}
        ]
        
        return await self.send_embed(
            title="🚀 Deployment Started",
            description=f"Deploying version {version} to {environment}",
            color=0x2ecc71,
            fields=fields
        )
    
    async def _check_rate_limit(self) -> bool:
        """
        Check if we're within Discord rate limits.
        
        Returns:
            True if request can proceed
        """
        now = datetime.now()
        
        if now < self._rate_limit_reset:
            if self._rate_limit_remaining <= 0:
                return False
        else:
            # Reset rate limit counter
            self._rate_limit_remaining = 5
            self._rate_limit_reset = now
        
        self._rate_limit_remaining -= 1
        return True


async def main():
    """Example usage of DiscordNotifier."""
    async with DiscordNotifier(channel_id="deployment-alerts") as notifier:
        # Send simple message
        await notifier.send_message("Deployment starting...")
        
        # Send deployment notification
        await notifier.send_deployment_notification(
            version="v1.2.3",
            environment="production",
            changes=[
                "Fixed authentication bug",
                "Added new dashboard features",
                "Performance improvements"
            ]
        )
        
        # Send error alert
        try:
            # Simulate error
            raise ValueError("Something went wrong")
        except Exception as e:
            await notifier.send_error_alert(
                error_message=str(e),
                stack_trace=str(e.__traceback__),
                context={"user_id": "12345", "action": "checkout"}
            )


if __name__ == "__main__":
    asyncio.run(main())
