from state import State
from action import Action
from action import *
from agent import Agent
import numpy as np
import math

stamina_area = [19, 39, 59, 79, 100]
len_stamina_area = len(stamina_area)
# starting point of state_ids
state_maps = {'field':0, 'wall':len_stamina_area, 
              'highair':len_stamina_area*2, 
              'lowair':len_stamina_area*3, 
              'goal':len_stamina_area*4, 'death':len_stamina_area*4+1}

def vector_size(vec):
    return math.sqrt(np.dot(vec, vec))

def norm(vec):
    _size_ = vector_size(vec)
    return vec / _size_ if _size_ != 0 else 0

def EuclideanDistance(pos1, pos2):
    pos3 = pos2 - pos1
    return math.sqrt(np.dot(pos3, pos3))


class Environment:
    # agent : agent object
    # map_info : 2d array to represent current map's heights
    # goal_position : position object
    # num_states : number of states
    # num_actions : number of actions
    # consume_stamina_info : stamina consume amount / fps with respect to action id(integer)
    # fall_damage : damage that reduces agents' HP when he fall.
    # fall_min_height : the height that can make damage
    # MAX_timestep : cutting size
    # MAX_stamina : maximum stamina value that agent can have
    # waiting_time : time until gain next action, unit_time=sec
    # parachute_height : minimum height that agent can unfold his parachute.
    def __init__(self, agent, map_info, goal_position,
                 num_states, num_actions, 
                 state_ids, action_ids, 
                 fall_damage,
                 fall_min_height,
                 MAX_timestep=500,
                 MAX_stamina=200, 
                 unit_time=1.3, 
                 parachute_height=3,
                 gravitial_acc=9.8,
                 climb_angle=60,
                 gliding_down=10.0):
        self.initial_agent = agent
        self.agent = None
        #State(remained_distance, state_id, state_no, spend_time=0)
        self.initial_state = State(EuclideanDistance(self.initial_agent.get_current_position(), goal_position), state_id='field',
                                    state_no=len_stamina_area-1)
        self.state = self.initial_state
        self.map_info = map_info
        self.goal_position = goal_position
        self.num_states = num_states
        self.num_actions = num_actions
        self.state_ids = state_ids
        self.action_ids = action_ids
        #self.consume_stamina_info = consume_stamina_info
        self.fall_damage = fall_damage
        self.fall_min_height = fall_min_height
        self.MAX_timestep = MAX_timestep
        self.MAX_stamina = MAX_stamina
        self.unit_time = unit_time
        self.parachute_height = parachute_height
        self.g = np.array([0., gravitial_acc, 0.])
        self.climb_angle = climb_angle
        self.gliding_down = np.array([0., gliding_down, 0.])
        self.dataset = []


    def inBound(self, x, z):
        return not (x < 0 or x >= len(self.map_info) or z < 0 or z >= len(self.map_info[0]))

    def isWall(self, x1, z1, x2, z2):
        tangent = (self.map_info[x1, z1] - self.map_info[x2, z2]) / EuclideanDistance(np.array([x1, 0, z1]), np.array([x2, 0, z2]))
        angle = np.arctan(abs(tangent))

        if angle >= self.climb_angle * np.pi / 180:
            return True

        return False

    def cal_next_pos(self, state, action):
        next_pos = self.agent.get_current_position()
        next_state_id = state.id
        if state.id == 'death' or state.id == 'goal' or action.action_id == 'Wait':
            return next_pos, state.id
        elif state.id == 'air' and action.action_id != None and 'j' in action.action_id:
            next_state_id = 'parachute'
            # parachute mode on
            
        """
            TODO : 현재 action에 모든 velocity 정보가 담겨 있다고 가정된 상태.
            그러나 현재 state에 따라서 그 velocity도 당연히 달라져야 한다
            이를테면 air상태와 parachute의 상태는 달라지기 때문에 parachute 모드일 때 velocity 조정 필요
            또한, 이 method에 들어온 시점에서 field가 아닌 state일 때에 대한 물리 옵션 확인 필요
        """
        # 60 frame으로 나눠서 충돌 테스트
        t = action.acting_time / 60
        
        prev_pos = self.agent.get_current_position()
        v_xz = np.array([action.velocity[0], 0., action.velocity[2]])
        v_y = np.array([0., action.velocity[1], 0.])

        y_err = 0.1
        while t <= action.acting_time:
            if self.inBound(next_pos[0], next_pos[2]):
                return next_pos, 'death'
            next_pos = next_pos + v_xz*t
            if next_state_id == 'air' or next_state_id == 'parachute':
                next_pos += (v_y - self.g*t)*t

            x, y, z = next_pos[0], next_pos[1], next_pos[2]

            if next_state_id == 'air' or next_state_id == 'parachute':
                if abs(y, self.map_info[x, z]) <= y_err:
                    next_pos[1] = self.map_info[x, z]
                    next_state_id = 'field'
                    if action.input_key != None and 'j' in action.input_key:
                        # jump action end.
                        break
                    
            if self.isWall(prev_pos[0], prev_pos[2], next_pos[0], next_pos[2]) == True:
                next_state_id = 'wall'
                break
            prev_pos = np.copy(next_pos)
            t += action.acting_time / 60

        return next_pos, next_state_id
    
    def state_transition(self, state, action):
        if state.id == 'death' or state.id == 'goal':
            return state, self.agent.get_current_pos()
        y = self.agent.pos[1]
        next_pos, next_state_id = self.cal_next_pos(state, action)
        nx, ny, nz = next_pos[0], next_pos[1], next_pos[2]
        
        def calc_fall_damage(y, ny):
            fall_height = y - ny - self.fall_min_height
            return 0 if fall_height <= 0 else fall_height * self.fall_damage
        
        # acting_time만큼 지날 동안 action.stamina_consume을 소모
        stamina = self.agent.stamina - action.stamina_consume * (base_acting_time / action.acting_time)
        stamina = int(stamina)
        if stamina <= 0:
            stamina = 0
        elif stamina > self.MAX_stamina:
            stamina = self.MAX_stamina

        remained_distance = EuclideanDistance(next_pos, self.goal_position)
        
        #next_state = State(remained_distance, state_id, spend_time=state.spend_time+self.unit_time)
        if self.inBound(nx, nz) == True and (y == ny or ny == self.map_info[int(nx), int(nz)]):
            next_state_id  = state.id
            self.agent.HP -= calc_fall_damage(y, ny)
            if self.agent.HP <= 0:
                next_state_id = 'death'
        
        next_state_no = state.no
        if next_state_id != 'death' and next_state_id != 'goal':
            for i in range(len_stamina_area):
                if stamina / self.MAX_stamina * 100.0 <= stamina_area[i]:
                    next_state_no = state_maps[next_state_id] + i
                    break
                
        #self.agent.action = action
        #self.agent.update_position(nx, ny, nz)
        next_state = State(remained_distance, next_state_id, next_state_no, spend_time=state.spend_time+self.unit_time)
        
        return next_state, next_pos
    
    
    def reward(self, state, action):
        next_state, next_pos = self.state_transition(state, action)
        deltaDistance = next_state.remained_distance - state.remained_distance
        
        return -deltaDistance, next_state, next_pos
    
    def get_random_action(self):
        _keys_ = list(self.action_ids.keys())
        key = int(np.random.randint(len(_keys_), size=1))
        return _keys_[key], self.action_ids[_keys_[key]]
    
    def make_scenarios(self, n=10):
        complete = 0
        tle_cnt = 0
        # print('action_id["Wait"] =', self.action_ids['Wait'])
        self.agent = Agent.from_agent(self.initial_agent)
        state = State.from_state(self.initial_state)
        while complete < n:
            scenario = []
            self.agent.Update(self.initial_agent)
            state.Update(self.initial_state)
            action = self.agent.action = Action(action_id=self.action_ids['Wait'], velocity=np.array([0.,0.,0.]))
            for t in range(self.MAX_timestep):
                if action.input_key != None and 'j' in action.input_key:
                    print('Tried to jump.')

                r, ns, next_pos = self.reward(state, action) # copy, not ref
                scenario.append([r, state.no, action.action_id])

                if t == self.MAX_timestep - 1:
                    tle_cnt += 1
                    print('Time over.')
                    print('failed:agent({}) / goal({})'.format(self.agent.get_current_position(), self.goal_position))
                    break

                # calculate next situation
                state = ns  # ok
                if state.id == 'death' or state.id == 'goal':
                    break
                next_key_input, next_action_id = self.get_random_action()
                stamina_consume = base_stamina_consume # 회복수치, -4.8
                acting_time = base_acting_time # 1.3sec
                if state.id == 'air':
                    stamina_consume = 0
                elif state.id == 'field':
                    if 's' in next_key_input:
                        stamina_consume = 20
                        acting_time = 1
                    if 'j' in next_key_input:
                        stamina_consume = 1 if stamina_consume == -4.8 else stamina_consume + 1
                elif state.id == 'wall':
                    if 'j' in next_key_input:
                        stamina_consume = 25
                elif state.id == 'parachute':
                    stamina_consume = 2
                    acting_time = 1
                elif state.id == 'goal' or state.id == 'death':
                    if state.id == 'death':
                        print('You died.')
                    break
                
                

                self.agent.update_position(next_pos)
                # return value of action_update is newly constructed.
                # So, it is okay.
                self.agent.dir = action.action_update(next_action_id, next_key_input, stamina_consume, acting_time, self.agent.dir)
                self.agent.action.Update(action)
            # steps ended.

            if state.id == 'goal':
                r_t = 0
                len_scenario = len(scenario)
                for t in range(1, len_scenario):
                    r_t += scenario[len_scenario - t][0]
                    scenario[len_scenario - t][0] = r_t # calculate return to go
                self.dataset.append(scenario)
                complete += 1
                print('complete {} / {}'.format(complete, n))

            if complete == 0 and tle_cnt >= n * n:
                print('Failed.\nIt needs to add Time-steps.')
                break
        
        return self.dataset
    
    
    def reset(self, dataset_initialize=False):
        #self.agent = self.initial_agent
        if dataset_initialize == True:
            self.dataset = []
    
    
    
    