<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.view');

class DbAppViewEntry extends JView {

  function display($tpl = null) {

    //$projects = $this->get('Projects');
    //$this->assignRef('projects', $projects);
    //$clients = $this->get('Clients');
    //$this->assignRef('clients', $clients);
    $mailtasks = $this->get('Mailtasks');
    $this->assignRef('mailtasks', $mailtasks);
    $buptasks = $this->get('Buptasks');
    $this->assignRef('buptasks', $buptasks);

    if (count($errors = $this->get('Errors'))) {
      JError::raiseError(500, implode('<br />', $errors));
      return false;
    }

    parent::display($tpl);
  }

}
?>
