<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.controller');

class DbAppControllerContact extends JController {

  public function save() {
    $data = JRequest::get('POST');
    $model = $this->getModel('project');
JError::raiseWarning('500', JText::_('[from site/controllers/client.php]C ' . JRequest::getVar('c')));
    if (!$model->save($data)) { 
      $message = JText::_('Contact saved');
    } else {
      $message = JText::_('Contact saved');
      $message .= ' [' . $model->getError() . ']';
    }
    $this->setRedirect('index.php?option=com_dpapp', $message);

  }

}
