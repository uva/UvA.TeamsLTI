<template>
  <div >
    <h1>Microsoft Teams <div>{{ course.name }}</div></h1>
    

    <div v-if="course.teams.length == 0">
      There are currently no teams for this course. 
    </div>
    <div v-if="course.teams.length > 0">
      The following team{{ course.teams.length > 1 ? "s have" : " has" }} been created:
      <div class="team-block" v-for="team in course.teams" :key="team.id">
        <a class="edit-link" href="#" @click.prevent="$emit('edit', team)">Edit</a>
        <a :href="team.url">{{ team.name }}</a>
        <div v-if="team.contexts[0].type == ContextType.Course">Entire course</div>
        <div v-if="team.contexts[0].type == ContextType.Section">{{ team.contexts.length }} section{{ team.contexts.length == 1 ? "": "s" }}</div>
      </div>
    </div>
    <button @click="newTeam">New team</button> <button v-if="course.teams.length > 0" @click="sync">Sync members</button>
  </div>
  <LoadingScreen text="Synchronizing" v-if="isSyncing" />
</template>

<script lang="ts">
import { Options, Vue } from 'vue-class-component';
import { ContextType, CourseInfo, Team } from '@/models/CourseInfo';
import LoadingScreen from './LoadingScreen.vue';
import axios from 'axios';

@Options({
  props: {
    course: Object
  },
  components: { LoadingScreen }
})
export default class TeamList extends Vue {
  course!: CourseInfo;
  ContextType = ContextType;
  isSyncing = false;

  newTeam(): void {
      const team: Team = {
          name: this.course.name,
          contexts: [{ type: ContextType.Course, id: -1 }],
          channels: [],
          allowChannels: true,
          allowPrivateChannels: true,
          createSectionChannels: false,
          groupSetIds: []
      }
      this.$emit('edit', team);
  }

  sync(): void {
    this.isSyncing = true;
    axios.post('/CourseInfo/Sync').then(s => this.isSyncing = false);
  }
}
</script>

<style scoped lang="scss">
  @use '../variables' as *;

  h1 > div {
    font-size: 18px;
    margin-top: -5px;
    color: #777;
    font-weight: normal;
  }

  button {
      margin-top: 20px;
  }

  .team-block {
    padding: 6px 7px;
    padding-left: 9px;
    width: 270px;
    outline: 1px solid #aaa;
    margin-top: 7px;
    position: relative;

    div {
      font-size: 13px;
    }

    &::before {
      content: "";
      position: absolute;
      left: 0;
      top: 0;
      bottom: 0;
      width: 3px;
      background: $primary-color;
    }
  }

  .edit-link {
    float: right;
    font-size: 13px;
  }
</style>
