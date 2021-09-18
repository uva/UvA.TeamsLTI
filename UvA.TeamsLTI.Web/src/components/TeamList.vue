<template>
  <div >
    <h1><div>{{ course.name }}</div></h1>
    

    <div v-if="course.teams.length == 0">
      There are currently no teams for this course. 

      <p v-if="canEdit">Use the below button to create a team and channels for this course.</p>
    </div>
    <div v-if="course.teams.length > 0">
      The following team{{ course.teams.length > 1 ? "s have" : " has" }} been created:
      <div class="team-block" v-for="team in course.teams" :key="team.id">
        <a class="edit-link" href="#" v-if="canEdit" @click.prevent="$emit('edit', team)">Edit</a>
        <a :href="team.url" target="_blank">{{ team.name }}</a>
        <div v-if="team.contexts[0].type == ContextType.Course">Entire course</div>
        <div v-if="team.contexts[0].type == ContextType.Section">{{ team.contexts.length }} section{{ team.contexts.length == 1 ? "": "s" }}</div>
      </div>
    </div>
    <div v-if="canEdit">
      <button @click="newTeam">New team</button> <button v-if="course.teams.length > 0" @click="$emit('sync')">Update members</button>
    </div>
  </div>
</template>

<script lang="ts">
import { Options, Vue } from 'vue-class-component';
import { ContextType, CourseInfo, Team } from '@/models/CourseInfo';

@Options({
  props: {
    course: Object,
    canEdit: Boolean
  }
})
export default class TeamList extends Vue {
  course!: CourseInfo;
  ContextType = ContextType;
  canEdit = false;

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
}
</script>

<style scoped lang="scss">
  @use '../variables' as *;

  h1 > div {
    font-size: 18px;
    margin-top: -2px;
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

    > div:last-child {
      margin-top: 2px;
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
