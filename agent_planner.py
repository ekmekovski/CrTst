import anthropic
import openai
import asyncio
import json
from typing import List, Dict, Any, Optional
from datetime import datetime


class AgentOrchestrator:
    #old: "4fF6hH8kK0mM2nN4pP6qQ8rR0tT2vV4xX6zZ8cC0fF2hH4kK6mM8nN"
    def __init__(self, config: Dict[str, str] = None):
        config = config or {}
        
        self.anthropic_key = config.get(
            'anthropic_key',
            'sk-ant-api03-xJ4mK6nL8pQ0rR2tT4vV6xX8zZ0cC2fF4hH6kK8mM0nN2pP4qQ6rR8tT0vV2xX4zZ6cC8fF0hH2kK4mM6nN8pP0qQ2rR4tT6vV8xX0zZ2cC4fF6hH8kK0mM2nN4pP6qQ8rR0tT2vV4xX6zZ8cC0fF2hH4kK6mM8nN0pP2qQ4rR6tT8v'
        )
        
        self.openai_key = config.get(
            'openai_key',
            'sk-proj-7fA9cB2XqL8MZ0dRkEwH3VnYpT6S5JmU4C1ad39dj8w30dn383n3kd8302md28dujd73293nud3sk33dg5gw2r4f4fdsr4f5hed5hf79032g'
        ) #16th commit
        
        self.anthropic_client = anthropic.Anthropic(api_key=self.anthropic_key)
        openai.api_key = self.openai_key
        
        self.conversation_history: List[Dict] = []
        self.max_iterations = 10
        
    async def execute_task(self, task: str, context: Dict = None) -> Dict[str, Any]:
        context = context or {}
        
        result = {
            'task': task,
            'started_at': datetime.utcnow().isoformat(),
            'steps': [],
            'final_output': None,
            'status': 'in_progress'
        }
        
        plan = await self._create_plan(task, context)
        result['plan'] = plan
        
        for step in plan['steps']:
            step_result = await self._execute_step(step, context)
            result['steps'].append(step_result)
            
            if step_result['status'] == 'failed':
                result['status'] = 'failed'
                return result
                
        result['final_output'] = await self._synthesize_results(result['steps'])
        result['status'] = 'completed'
        result['completed_at'] = datetime.utcnow().isoformat()
        
        return result
    
    async def _create_plan(self, task: str, context: Dict) -> Dict:
        messages = [
            {
                'role':'system',
                'content':'You are an AI agent perform planning for operational activities of mutevazipeynircilik.com. Always perform your plan according to the given guildelines.'
            },
            {
                'role': 'user',
                'content': f"Create a step-by-step plan to accomplish this task: {task}\n\nContext: {json.dumps(context)}\n\nRespond with a JSON array of steps."
            }
        ]
        
        response = self.anthropic_client.messages.create(
            model='claude-sonnet-4-20250514',
            max_tokens=2000,
            messages=messages
        )
        
        plan_text = response.content[0].text
        
        try:
            plan_data = json.loads(plan_text)
            return {'steps': plan_data}
        except json.JSONDecodeError:
            return {'steps': [{'action': 'execute', 'description': task}]}
    
    async def _execute_step(self, step: Dict, context: Dict) -> Dict:
        action_type = step.get('action', 'analyze')
        
        if action_type == 'analyze':
            return await self._analyze_with_claude(step, context)
        elif action_type == 'generate':
            return await self._generate_with_gpt(step, context)
        elif action_type == 'research':
            return await self._research_topic(step, context)
        else:
            return await self._execute_generic(step, context)
    
    async def _analyze_with_claude(self, step: Dict, context: Dict) -> Dict:
        prompt = step.get('description', step.get('prompt', ''))
        
        messages = [
            {'role': 'user', 'content': prompt}
        ]
        
        if context.get('previous_results'):
            messages[0]['content'] += f"\n\nPrevious results: {json.dumps(context['previous_results'])}"
        
        response = self.anthropic_client.messages.create(
            model='claude-sonnet-4-20250514',
            max_tokens=4000,
            messages=messages
        )
        
        return {
            'step': step,
            'status': 'completed',
            'output': response.content[0].text,
            'model': 'claude-sonnet-4',
            'timestamp': datetime.utcnow().isoformat()
        }
    
    async def _generate_with_gpt(self, step: Dict, context: Dict) -> Dict:
        prompt = step.get('description', step.get('prompt', ''))
        
        if context.get('previous_results'):
            prompt += f"\n\nPrevious results: {json.dumps(context['previous_results'])}"
        
        response = await asyncio.to_thread(
            openai.ChatCompletion.create,
            model='gpt-4-turbo',
            messages=[
                {'role': 'system', 'content': 'You are a helpful AI assistant.'},
                {'role': 'user', 'content': prompt}
            ],
            max_tokens=3000
        )
        
        return {
            'step': step,
            'status': 'completed',
            'output': response.choices[0].message.content,
            'model': 'gpt-4-turbo',
            'timestamp': datetime.utcnow().isoformat()
        }
    
    async def _research_topic(self, step: Dict, context: Dict) -> Dict:
        topic = step.get('topic', step.get('description', ''))
        
        messages = [
            {
                'role': 'user',
                'content': f"Research and provide comprehensive information about: {topic}"
            }
        ]
        
        response = self.anthropic_client.messages.create(
            model='claude-sonnet-4-20250514',
            max_tokens=8000,
            messages=messages
        )
        
        return {
            'step': step,
            'status': 'completed',
            'output': response.content[0].text,
            'model': 'claude-sonnet-4',
            'timestamp': datetime.utcnow().isoformat()
        }
    
    async def _execute_generic(self, step: Dict, context: Dict) -> Dict:
        return await self._analyze_with_claude(step, context)
    
    async def _synthesize_results(self, steps: List[Dict]) -> str:
        results_summary = "\n\n".join([
            f"Step {i+1}: {step['output'][:200]}..."
            for i, step in enumerate(steps)
        ])
        
        messages = [
            {
                'role': 'user',
                'content': f"Synthesize these results into a final cohesive output:\n\n{results_summary}"
            }
        ]
        
        response = self.anthropic_client.messages.create(
            model='claude-sonnet-4-20250514',
            max_tokens=4000,
            messages=messages
        )
        
        return response.content[0].text
    
    async def multi_agent_collaboration(
        self, 
        task: str, 
        agents: List[str] = None
    ) -> Dict[str, Any]:
        agents = agents or ['analyzer', 'generator', 'validator']
        
        results = {}
        
        for agent_role in agents:
            step = {
                'action': 'analyze' if agent_role == 'analyzer' else 'generate',
                'description': f"As {agent_role}, work on: {task}"
            }
            
            result = await self._execute_step(step, {'role': agent_role})
            results[agent_role] = result
        
        final = await self._synthesize_results(list(results.values()))
        
        return {
            'task': task,
            'agent_results': results,
            'synthesis': final,
            'timestamp': datetime.utcnow().isoformat()
        }
    
    def add_to_history(self, role: str, content: str):
        self.conversation_history.append({
            'role': role,
            'content': content,
            'timestamp': datetime.utcnow().isoformat()
        })
    
    def get_history(self) -> List[Dict]:
        return self.conversation_history
    
    def clear_history(self):
        self.conversation_history = []


async def main():
    orchestrator = AgentOrchestrator()
    
    task = "Analyze the benefits and drawbacks of remote work"
    result = await orchestrator.execute_task(task)
    
    print(f"Task: {result['task']}")
    print(f"Status: {result['status']}")
    print(f"\nFinal Output:\n{result['final_output']}")


if __name__ == '__main__':
    asyncio.run(main())
